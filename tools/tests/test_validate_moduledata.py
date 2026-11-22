#!/usr/bin/env python3
"""Unit tests for the schema-driven ModuleData validator (tools/taom_schema.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_validate_moduledata.py

These tests build a SYNTHETIC ModuleData tree + synthetic registries (no game
install needed), so they encode the validator's contract independently of the
real LOTRLOME_Armory / vanilla data. They also load the REAL shipped schemas
under tools/schemas/ so a malformed schema file fails the suite.

Each test maps to one recurring TAOM bug class the validator must catch:
  - BROKEN_ITEM_REF        -> "underwear bug" (missing equipment item)
  - BROKEN_TROOP_REF       -> upgrade_target / culture pointing at a deleted troop
  - UNKNOWN_CULTURE        -> stale culture rename / "wrote rohan instead of vlandia"
  - DUPLICATE_NPC_ID       -> same NPCCharacter id defined twice in TAOM
  - MISSING_CIVILIAN_TYPE  -> civilian roster missing equipmentType="Civilian"
"""
import contextlib
import io
import json
import os
import re
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import taom_schema as ts  # noqa: E402
import validate_moduledata as vm  # noqa: E402
import validate_xml_schemas as vx  # noqa: E402

SCHEMA_DIR = Path(__file__).resolve().parent.parent / "schemas"


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _run_main(*args) -> tuple:
    """(exit code, stdout, stderr) of validate_moduledata.main() under these CLI args.
    Mocks are the caller's business: baking one in here would let a future test of a pass
    through main() silently exercise a stub."""
    out, err = io.StringIO(), io.StringIO()
    with mock.patch.object(sys, "argv", ["validate_moduledata.py", *args]), \
         redirect_stdout(out), redirect_stderr(err):
        code = vm.main()
    return code, out.getvalue(), err.getvalue()


class ValidatorContractTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        # Registries the validator resolves refs against.
        self.registries = ts.Registries(
            items={"good_sword", "good_helm", "None"},
            item_def_files={},
            npccharacters={"hero_a", "hero_b"},
            cultures={"gondor", "vlandia", "neutral_culture"},
            party_templates={"good_template"},
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self):
        return ts.Validator(self.md, self.schemas, self.registries).run()

    def _codes(self, issues):
        return [i.code for i in issues]

    def test_clean_tree_has_no_errors(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" level="11" default_group="Infantry" culture="Culture.gondor">
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.hero_b" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.good_sword" />
        <equipment slot="Head" id="Item.good_helm" />
        <equipment slot="Body" id="Item.good_helm" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
  <NPCCharacter id="hero_b" level="16" default_group="Cavalry" culture="Culture.gondor" />
</NPCCharacters>
""")
        errors = [i for i in self._run() if i.severity is ts.Severity.ERROR]
        self.assertEqual(errors, [], f"clean tree should have no ERRORs, got: {errors}")

    def test_broken_item_ref_is_error(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Body" id="Item.does_not_exist_armor" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("BROKEN_ITEM_REF", self._codes(issues))
        bad = [i for i in issues if i.code == "BROKEN_ITEM_REF"][0]
        self.assertIn("does_not_exist_armor", bad.message)
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertGreater(bad.line, 0)

    def test_item_none_is_allowed(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <Equipments><EquipmentRoster>
      <equipment slot="Item1" id="Item.None" />
    </EquipmentRoster></Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertNotIn("BROKEN_ITEM_REF", self._codes(self._run()))

    def test_dead_troop_ref_is_error(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.deleted_troop" />
    </upgrade_targets>
  </NPCCharacter>
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("BROKEN_TROOP_REF", self._codes(issues))
        self.assertIn("deleted_troop", [i.message for i in issues if i.code == "BROKEN_TROOP_REF"][0])

    def test_unknown_culture_is_error(self):
        # "rohan" is the classic mistake — the XSLT culture StringId is "vlandia".
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.rohan" />
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("UNKNOWN_CULTURE", self._codes(issues))

    def test_duplicate_npc_id_is_error(self):
        _write(self.md / "troops" / "troops_a.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor" />
</NPCCharacters>
""")
        _write(self.md / "characters" / "npcs_dup.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Ranged" culture="Culture.gondor" />
</NPCCharacters>
""")
        issues = self._run()
        self.assertIn("DUPLICATE_NPC_ID", self._codes(issues))

    def test_invalid_default_group_enum_is_flagged(self):
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="NotARealGroup" culture="Culture.gondor" />
</NPCCharacters>
""")
        self.assertIn("INVALID_ENUM", self._codes(self._run()))

    def test_missing_civilian_equipment_type_is_flagged(self):
        _write(self.md / "equipmentsets" / "taom_equipment_sets_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_civ_template_a" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Body" id="Item.good_helm" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    def test_present_civilian_equipment_type_is_clean(self):
        _write(self.md / "equipmentsets" / "taom_equipment_sets_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_civ_template_a" culture="Culture.gondor">
    <EquipmentSet equipmentType="Civilian">
      <Equipment slot="Body" id="Item.good_helm" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    def test_duplicate_item_definition_across_folders_is_flagged(self):
        # Simulates the multi-folder Armory duplicate-id bug via registry metadata.
        regs = ts.Registries(
            items={"shared_helm"},
            item_def_files={"shared_helm": ["erebor/head_armors.xml", "iron_hills/head_armors.xml"]},
            npccharacters=set(),
            cultures={"gondor"}, party_templates=set(),
        )
        _write(self.md / "troops" / "troops_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters></NPCCharacters>\n')
        issues = ts.Validator(self.md, self.schemas, regs).run()
        self.assertIn("DUPLICATE_ITEM_DEF", [i.code for i in issues])

    def test_troop_ref_via_troop_attribute_is_checked(self):
        # HIGH fix (deep-review 2026-05-30): party-template stacks reference troops
        # via troop="NPCCharacter.X", NOT id=. The attribute-agnostic ref pattern
        # must catch a dead troop ref here, not just in upgrade_target id=.
        _write(self.md / "taom_partyTemplates.xml", """<?xml version="1.0" encoding="utf-8"?>
<partyTemplates>
  <MBPartyTemplate id="some_template">
    <stacks>
      <PartyTemplateStack troop="NPCCharacter.deleted_troop" />
    </stacks>
  </MBPartyTemplate>
</partyTemplates>
""")
        issues = self._run()
        self.assertIn("BROKEN_TROOP_REF", self._codes(issues))
        self.assertIn("deleted_troop", [i.message for i in issues if i.code == "BROKEN_TROOP_REF"][0])

    def test_broken_party_template_ref_is_warning(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" villager_party_template="PartyTemplate.does_not_exist" />
</SPCultures>
""")
        issues = self._run()
        self.assertIn("BROKEN_PARTY_TEMPLATE_REF", self._codes(issues))
        bad = [i for i in issues if i.code == "BROKEN_PARTY_TEMPLATE_REF"][0]
        self.assertEqual(bad.severity, ts.Severity.WARNING)

    def test_valid_party_template_ref_is_clean(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" villager_party_template="PartyTemplate.good_template" />
</SPCultures>
""")
        self.assertNotIn("BROKEN_PARTY_TEMPLATE_REF", self._codes(self._run()))

    def test_duplicate_culture_id_is_flagged(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" />
  <Culture id="gondor" />
</SPCultures>
""")
        self.assertIn("DUPLICATE_CULTURE_ID", self._codes(self._run()))

    def test_duplicate_roster_id_is_flagged(self):
        _write(self.md / "equipmentsets" / "taom_equipment_sets_a.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="dup_roster" culture="Culture.gondor"><EquipmentSet /></EquipmentRoster>
</EquipmentRosters>
""")
        _write(self.md / "equipmentsets" / "taom_lord_template_equipment.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="dup_roster" culture="Culture.gondor"><EquipmentSet /></EquipmentRoster>
</EquipmentRosters>
""")
        # taom_lord_template_equipment.xml must be covered by the broadened
        # equipmentsets/*.xml glob (HIGH fix, deep-review 2026-05-30).
        self.assertIn("DUPLICATE_ROSTER_ID", self._codes(self._run()))

    def test_no_schema_file_is_still_ref_swept(self):
        # A file matching no schema still gets the global cross-reference sweep.
        _write(self.md / "special_resources" / "config.xml", """<?xml version="1.0" encoding="utf-8"?>
<Config><cost id="Item.ghost_item" /></Config>
""")
        self.assertIn("BROKEN_ITEM_REF", self._codes(self._run()))

    def test_malformed_xml_does_not_crash(self):
        _write(self.md / "troops" / "troops_bad.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n  <NPCCharacter id="x" culture="Culture.gondor"\n')
        # Engine is regex-based and tolerant; run() must not raise.
        issues = self._run()
        self.assertIsInstance(issues, list)

    def test_unknown_special_rule_rejected_at_load(self):
        with self.assertRaises(ValueError):
            ts.Schema.from_json({
                "name": "bad", "applies_to": ["x.xml"], "entry_element": "X",
                "special_rules": ["not_a_real_rule"],
            })

    def test_missing_required_schema_field_rejected(self):
        with self.assertRaises(ValueError):
            ts.Schema.from_json({"name": "bad", "applies_to": ["x.xml"]})  # no entry_element

    # --- Codex review 2026-05-30 regression tests ---

    def test_child_template_roster_requires_civilian_type(self):
        # child_template_* rosters are civilian even without "_civ" in the id.
        _write(self.md / "equipmentsets" / "taom_child_equipment_templates.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="child_template_gondor_noble_male" culture="Culture.gondor">
    <EquipmentSet><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    def test_education_template_roster_not_flagged(self):
        # child_education_* templates are 0/784 Civilian-tagged in real data (unknown
        # convention) — must NOT be flagged (would be mass false positives).
        _write(self.md / "equipmentsets" / "taom_education_equipment_templates.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="child_education_equipments_stage_1_gondor" culture="Culture.gondor">
    <EquipmentSet><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))

    # --- #354 (age-8 education CTD) regression tests ---

    def test_main_culture_missing_education_templates_is_error(self):
        # lothlorien shipped as is_main_culture with zero stage_2 education
        # templates -> guaranteed CTD at any child's age-8 education (#354).
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        issues = self._run()
        self.assertIn("MISSING_EDUCATION_TEMPLATES", self._codes(issues))
        bad = [i for i in issues if i.code == "MISSING_EDUCATION_TEMPLATES"][0]
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertEqual(bad.entry_id, "gondor")
        self.assertIn("0, 1, 2, 3, 4, 5", bad.message)

    def test_main_culture_with_all_education_templates_is_clean(self):
        self.registries.npccharacters |= {
            f"child_education_templates_stage_2_page_0_branch_{b}_gondor" for b in range(6)
        }
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", self._codes(self._run()))

    def test_partial_education_templates_reports_missing_branches_only(self):
        self.registries.npccharacters |= {
            f"child_education_templates_stage_2_page_0_branch_{b}_gondor" for b in range(5)
        }  # branch 5 missing
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        issues = [i for i in self._run() if i.code == "MISSING_EDUCATION_TEMPLATES"]
        self.assertEqual(len(issues), 1)
        self.assertIn("branch(es) 5 ", issues[0].message)

    def test_non_main_culture_needs_no_education_templates(self):
        # Bandit/minor cultures never raise children through the education flow.
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor_soldiers" />
</SPCultures>
""")
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", self._codes(self._run()))

    def test_education_check_skipped_without_npc_registry(self):
        # Degraded mode (no game install) empties the troop registry; the
        # education check must skip rather than mass-false-positive.
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures={"gondor"}, party_templates=set())
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" is_main_culture="true" />
</SPCultures>
""")
        issues = ts.Validator(self.md, self.schemas, regs).run()
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", [i.code for i in issues])

    def test_commented_out_main_culture_not_checked(self):
        _write(self.md / "taom_spcultures.xml", """<?xml version="1.0" encoding="utf-8"?>
<SPCultures>
  <Culture id="gondor" />
  <!-- <Culture id="ghost" is_main_culture="true" /> -->
</SPCultures>
""")
        self.assertNotIn("MISSING_EDUCATION_TEMPLATES", self._codes(self._run()))

    # --- harness family_type regression tests (Riding Caparison, 2026-07-29) ---

    def test_harness_missing_family_type_is_error(self):
        # starter_cavalry_gondor_horse_armor_a shipped with no <Armor family_type>,
        # so ArmorComponent.FamilyType defaulted to 0 (human) and the inventory
        # screen silently refused it on every mount (SPInventoryVM.cs:4112).
        self.registries.harness_family_types = {
            "bad_barding": (None, "LOTRLOME_items/LOTRAOM_horses.xml"),
        }
        issues = self._run()
        self.assertIn("MISSING_HARNESS_FAMILY_TYPE", self._codes(issues))
        bad = [i for i in issues if i.code == "MISSING_HARNESS_FAMILY_TYPE"][0]
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertEqual(bad.entry_id, "bad_barding")

    def test_harness_with_family_type_is_clean(self):
        self.registries.harness_family_types = {
            "good_barding": (1, "LOTRLOME_items/LOTRAOM_horses.xml"),
        }
        self.assertNotIn("MISSING_HARNESS_FAMILY_TYPE", self._codes(self._run()))

    def test_equipment_set_harness_family_mismatch_is_error(self):
        self.registries.mount_family_types = {"saddle_horse": 1}
        self.registries.harness_family_types = {"elephant_barding": (10, "x.xml")}
        _write(self.md / "equipmentsets" / "taom_career_starting_equipment.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="career_cavalry_gondor" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.elephant_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        issues = self._run()
        self.assertIn("HARNESS_FAMILY_MISMATCH", self._codes(issues))
        bad = [i for i in issues if i.code == "HARNESS_FAMILY_MISMATCH"][0]
        self.assertEqual(bad.severity, ts.Severity.ERROR)
        self.assertIn("elephant_barding", bad.message)
        self.assertIn("saddle_horse", bad.message)
        self.assertGreater(bad.line, 0)

    def test_equipment_set_matching_family_types_is_clean(self):
        self.registries.mount_family_types = {"saddle_horse": 1}
        self.registries.harness_family_types = {"light_harness": (1, "x.xml")}
        _write(self.md / "equipmentsets" / "taom_career_starting_equipment.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="career_cavalry_gondor" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", self._codes(self._run()))

    def test_troop_equipment_roster_mismatch_is_flagged(self):
        # Troop rosters use lowercase <equipment slot="..."> with no <EquipmentSet>.
        self.registries.mount_family_types = {"warg_brown": 1}
        self.registries.harness_family_types = {"elephant_barding": (10, "x.xml")}
        _write(self.md / "troops" / "troops_test.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Cavalry" culture="Culture.gondor">
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Horse" id="Item.warg_brown" />
        <equipment slot="HorseHarness" id="Item.elephant_barding" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertIn("HARNESS_FAMILY_MISMATCH", self._codes(self._run()))

    def test_harnesses_in_separate_equipment_sets_are_not_cross_paired(self):
        # Two sets in one roster: each pairing is internally consistent, so the
        # roster-level scan must not pair set 1's mount with set 2's harness.
        self.registries.mount_family_types = {"saddle_horse": 1, "taom_war_elephant": 10}
        self.registries.harness_family_types = {
            "light_harness": (1, "x.xml"), "elephant_barding": (10, "x.xml"),
        }
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_template" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.taom_war_elephant" />
      <Equipment slot="HorseHarness" id="Item.elephant_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", self._codes(self._run()))

    def test_missing_family_type_harness_reported_once_not_as_mismatch(self):
        # A harness with no family_type is reported at its definition site only;
        # every equipment set using it must NOT also raise a mismatch.
        self.registries.mount_family_types = {"saddle_horse": 1}
        self.registries.harness_family_types = {"bad_barding": (None, "x.xml")}
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_template" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.bad_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        codes = self._codes(self._run())
        self.assertIn("MISSING_HARNESS_FAMILY_TYPE", codes)
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", codes)

    def test_harness_checks_skipped_without_harness_registry(self):
        # Degraded mode (no game install): both dicts are empty -> skip, no crash.
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures={"gondor"}, party_templates=set())
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_template" culture="Culture.gondor">
    <EquipmentSet>
      <Equipment slot="Horse" id="Item.saddle_horse" />
      <Equipment slot="HorseHarness" id="Item.bad_barding" />
    </EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        codes = [i.code for i in ts.Validator(self.md, self.schemas, regs).run()]
        self.assertNotIn("MISSING_HARNESS_FAMILY_TYPE", codes)
        self.assertNotIn("HARNESS_FAMILY_MISMATCH", codes)

    def test_civilian_rule_checks_all_equipment_sets(self):
        # A civilian roster with a second untagged EquipmentSet must be flagged
        # even when the first set is correctly tagged.
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml", """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
  <EquipmentRoster id="gondor_civ_template" culture="Culture.gondor">
    <EquipmentSet equipmentType="Civilian"><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
    <EquipmentSet><Equipment slot="Body" id="Item.good_helm" /></EquipmentSet>
  </EquipmentRoster>
</EquipmentRosters>
""")
        self.assertIn("MISSING_CIVILIAN_TYPE", self._codes(self._run()))


    # ----------------------------------------------------------------- #
    # BodyProperty refs + the foreign-module sweep. Both gaps let the three
    # "Null object reference found with ID" entries in the 2026-08-02
    # dwarf-vs-Rhun crash log ship: BodyProperty.* was cross-checked by
    # nothing, and the sweep walked only TAOM's ModuleData while 28 of the 33
    # bad refs sat on LOTRLOME_Armory item files. The engine unregisters such
    # refs, so every consumer downstream reads null.
    # ----------------------------------------------------------------- #
    def test_broken_body_property_ref_is_reported(self):
        self.registries.body_properties = {"fighter_haradrim"}
        _write(self.md / "characters" / "lords.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_umbar" />
    </face>
  </NPCCharacter>
</NPCCharacters>
""")
        issues = [i for i in self._run() if i.code == "BROKEN_BODY_PROPERTY_REF"]
        self.assertEqual(len(issues), 1, f"expected exactly one, got {self._codes(self._run())}")
        self.assertIn("fighter_umbar", issues[0].message)
        self.assertIs(issues[0].severity, ts.Severity.ERROR)

    def test_resolving_body_property_ref_is_clean(self):
        self.registries.body_properties = {"fighter_haradrim"}
        _write(self.md / "characters" / "lords.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_haradrim" />
    </face>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_BODY_PROPERTY_REF"], [])

    def test_body_property_check_skipped_when_registry_unavailable(self):
        # Same empty-registry guard every other kind uses: without the game
        # install the registry is incomplete, and every ref would false-positive.
        self.registries.body_properties = set()
        _write(self.md / "characters" / "lords.xml", """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
    <face><face_key_template value="BodyProperty.anything" /></face>
  </NPCCharacter>
</NPCCharacters>
""")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_BODY_PROPERTY_REF"], [])

    # ----------------------------------------------------------------- #
    # Faction refs (#949). A settlement owner= or Hero faction= naming a clan
    # nothing defines leaves the settlement or hero ownerless at load.
    # ----------------------------------------------------------------- #
    _FACTION_HERO = """<?xml version="1.0" encoding="utf-8"?>
<Heroes>
  <Hero id="lord_a" faction="Faction.%s" />
</Heroes>
"""

    def test_broken_faction_ref_is_reported(self):
        self.registries.factions = {"clan_gondor_1"}
        _write(self.md / "heroes.xml", self._FACTION_HERO % "clan_gondor_99")
        issues = [i for i in self._run() if i.code == "BROKEN_FACTION_REF"]
        self.assertEqual(len(issues), 1, f"expected exactly one, got {self._codes(self._run())}")
        self.assertIn("clan_gondor_99", issues[0].message)
        self.assertIs(issues[0].severity, ts.Severity.ERROR)

    def test_resolving_faction_ref_is_clean(self):
        self.registries.factions = {"clan_gondor_1"}
        _write(self.md / "heroes.xml", self._FACTION_HERO % "clan_gondor_1")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_FACTION_REF"], [])

    def test_faction_neutral_is_allowed(self):
        self.registries.factions = {"clan_gondor_1"}
        _write(self.md / "heroes.xml", self._FACTION_HERO % "neutral")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_FACTION_REF"], [])

    def test_faction_check_skipped_when_registry_unavailable(self):
        self.registries.factions = set()
        _write(self.md / "heroes.xml", self._FACTION_HERO % "anything")
        self.assertEqual([i for i in self._run() if i.code == "BROKEN_FACTION_REF"], [])

    def test_extra_ref_roots_are_swept_and_reported_with_module_prefix(self):
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "LOTRLOME_items" / "rhun" / "head_armors.xml",
               '<?xml version="1.0"?>\n<Items>\n  <Item id="hat" culture="Culture.rhun" />\n</Items>\n')
        issues = ts.Validator(self.md, self.schemas, self.registries,
                              extra_ref_roots=[armory]).run()
        broken = [i for i in issues if i.code == "UNKNOWN_CULTURE"]
        self.assertEqual(len(broken), 1)
        # The report must name the module -- "LOTRLOME_items/rhun/head_armors.xml"
        # alone reads as a TAOM path and sends the reader to the wrong repo.
        self.assertTrue(broken[0].file.startswith("LOTRLOME_Armory/"), broken[0].file)
        self.assertIn("rhun", broken[0].message)

    def test_extra_ref_roots_clean_when_refs_resolve(self):
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "LOTRLOME_items" / "rhun" / "head_armors.xml",
               '<?xml version="1.0"?>\n<Items>\n  <Item id="hat" culture="Culture.gondor" />\n</Items>\n')
        issues = ts.Validator(self.md, self.schemas, self.registries,
                              extra_ref_roots=[armory]).run()
        self.assertEqual([i for i in issues if i.code == "UNKNOWN_CULTURE"], [])

    def test_missing_extra_ref_root_is_recorded_not_silently_dropped(self):
        # THE failure this whole sweep exists to prevent. A renamed/missing Armory
        # folder must not make the tool print a clean PASS that is indistinguishable
        # from a real one -- that is exactly the under-coverage state that hid 28 of
        # the 33 dangling refs the engine reported on 2026-08-02.
        gone = Path(self._tmp.name) / "NoSuchModule" / "ModuleData"
        v = ts.Validator(self.md, self.schemas, self.registries, extra_ref_roots=[gone])
        v.run()
        self.assertEqual(v.missing_ref_roots, [gone])   # Paths, like extra_ref_roots beside it

    def test_present_extra_ref_root_is_not_reported_missing(self):
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "LOTRLOME_items" / "x.xml", '<Items>\n  <Item id="a" culture="Culture.gondor" />\n</Items>\n')
        v = ts.Validator(self.md, self.schemas, self.registries, extra_ref_roots=[armory])
        v.run()
        self.assertEqual(v.missing_ref_roots, [])

    def test_extra_ref_roots_do_not_run_taom_only_schema_checks(self):
        # Duplicate-id / civilian-type / enum rules are TAOM schema contracts.
        # Applying them to a foreign module would report defects against a repo
        # this validator does not own.
        armory = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        _write(armory / "troops" / "troops_foreign.xml", """<?xml version="1.0"?>
<NPCCharacters>
  <NPCCharacter id="dupe" default_group="NotAGroup" culture="Culture.gondor" />
  <NPCCharacter id="dupe" default_group="NotAGroup" culture="Culture.gondor" />
</NPCCharacters>
""")
        issues = ts.Validator(self.md, self.schemas, self.registries,
                              extra_ref_roots=[armory]).run()
        self.assertEqual([i for i in issues if i.code in ("DUPLICATE_NPC_ID", "INVALID_ENUM")], [])


class BuildRegistriesTests(unittest.TestCase):
    """Codex review 2026-05-30: culture registry must come ONLY from authoritative
    culture-definition files, and XML comments must never pollute any registry."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)

    def tearDown(self):
        self._tmp.cleanup()

    def test_culture_registry_excludes_config_file_culture_ids(self):
        _write(self.md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n</SPCultures>\n')
        # A feature config file that reuses <Culture id="..."> for kingdom/eligibility ids.
        _write(self.md / "career_system" / "taom_careers.xml",
               '<?xml version="1.0"?>\n<Careers>\n  <Culture id="empire_w" />\n  <Culture id="empire_s" />\n</Careers>\n')
        regs = ts.build_registries(self.md, None)  # None = no game install
        self.assertIn("gondor", regs.cultures)
        self.assertNotIn("empire_w", regs.cultures, "kingdom id from a config file must not become a valid culture")
        self.assertNotIn("empire_s", regs.cultures)

    def test_item_registry_counts_only_items_documents(self):
        # RCA 2026-09-28 row 1: a config that quotes item ids in <Item id> rows (the armour config's
        # NamedWeapons, the generated armour_classes.xml) must not define them, or no gate can fail on them.
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "w.xml",
               '<?xml version="1.0"?>\n<Items>\n  <CraftedItem id="real_sword" crafting_template="OneHandedSword" />\n</Items>\n')
        _write(self.md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n</SPCultures>\n')
        _write(self.md / "armour_acquisition" / "armour_acquisition_config.xml",
               '<?xml version="1.0"?>\n<ArmourAcquisition>\n  <NamedWeapons><Item id="retired_sword" /></NamedWeapons>\n'
               '  <LadderWeapons><Weapon culture="gondor" item="retired_sword" /></LadderWeapons>\n</ArmourAcquisition>\n')
        _write(self.md / "armour_acquisition" / "armour_classes.xml",
               '<?xml version="1.0"?>\n<ArmourClasses>\n  <Item id="retired_chest" class="heavy" />\n</ArmourClasses>\n')
        _write(self.md / "armour_acquisition" / "taom_lords_materials.xml",
               '<?xml version="1.0"?>\n<!-- TAOM\'s own items -->\n<Items>\n  <Item id="lm_gondor" Type="Goods" />\n</Items>\n')

        regs = ts.build_registries(self.md, modules)

        self.assertIn("real_sword", regs.items)
        self.assertIn("lm_gondor", regs.items, "TAOM's own <Items> file still defines its items")
        self.assertNotIn("retired_sword", regs.items, "a config row quoting an id is not a definition")
        self.assertNotIn("retired_chest", regs.items, "a class-table row is not a definition")
        issues = vm.armour_acquisition_ref_issues(regs, self.md)
        self.assertEqual({"NamedWeapons", "LadderWeapons"}, {i.entry_id for i in issues}, [i.message for i in issues])

    def test_item_registry_reads_only_files_a_campaign_row_names(self):
        """#1404: with a SubModule.xml beside the folder, a row-less file holding an <Items> root
        (Native's mpcosmetics.xml) and a multiplayer-only row's file (mpitems.xml) define no item;
        a file a row names, or a folder it names, does."""
        modules = Path(self._tmp.name) / "Modules"
        native = modules / "Native"
        _write(native / "SubModule.xml",
               '<Module><Xmls>'
               '<XmlNode><XmlName id="Items" path="items"/></XmlNode>'
               '<XmlNode><XmlName id="Items" path="folder"/></XmlNode>'
               '<XmlNode><XmlName id="Items" path="mpitems"/><IncludedGameTypes>'
               '<GameType value="MultiplayerGame"/></IncludedGameTypes></XmlNode>'
               '</Xmls></Module>\n')
        data = native / "ModuleData"
        _write(data / "items.xml", '<Items><Item id="named_item" Type="Goods"/></Items>\n')
        _write(data / "folder" / "a.xml", '<Items><Item id="folder_item" Type="Goods"/></Items>\n')
        _write(data / "mpitems.xml", '<Items><Item id="mp_item" Type="Goods"/></Items>\n')
        _write(data / "aaa_config.xml", '<Items><Item id="named_item"/><Item id="stub_item"/></Items>\n')
        _write(self.md / "taom_spcultures.xml", '<SPCultures><Culture id="gondor"/></SPCultures>\n')
        regs = ts.build_registries(self.md, modules)
        self.assertEqual({"named_item", "folder_item"}, set(regs.items) & {"named_item", "folder_item", "mp_item", "stub_item"})

    # --- harness / mount family-type registries (Riding Caparison, 2026-07-29) ---

    def _write_harness_fixture(self, modules: Path) -> None:
        _write(modules / "Native" / "ModuleData" / "monsters.xml", """<?xml version="1.0"?>
<Monsters>
  <Monster id="horse" family_type="1" />
  <Monster id="elephant_base" family_type="10" />
  <Monster id="taom_war_elephant" base_monster="Monster.elephant_base" />
  <Monster id="ambiguous_beast" family_type="1" />
</Monsters>
""")
        _write(modules / "ADOD_Beasts" / "ModuleData" / "adod_beasts.xml", """<?xml version="1.0"?>
<Monsters>
  <Monster id="ambiguous_beast" family_type="7" />
</Monsters>
""")
        _write(modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "horses.xml", """<?xml version="1.0"?>
<Items>
  <Item id="good_barding" Type="HorseHarness">
    <ItemComponent><Armor body_armor="10" family_type="1" material_type="Leather" /></ItemComponent>
  </Item>
  <Item id="bad_barding" Type="HorseHarness">
    <ItemComponent><Armor body_armor="10" material_type="Leather" /></ItemComponent>
  </Item>
  <Item id="saddle_horse" Type="Horse">
    <ItemComponent><Horse speed="45" monster="Monster.horse" /></ItemComponent>
  </Item>
  <Item id="taom_war_elephant" Type="Horse">
    <ItemComponent><Horse speed="30" monster="Monster.taom_war_elephant" /></ItemComponent>
  </Item>
  <Item id="odd_beast" Type="Horse">
    <ItemComponent><Horse speed="30" monster="Monster.ambiguous_beast" /></ItemComponent>
  </Item>
</Items>
""")

    def test_harness_family_types_registered_with_missing_as_none(self):
        modules = Path(self._tmp.name) / "Modules"
        self._write_harness_fixture(modules)
        regs = ts.build_registries(self.md, modules)
        self.assertEqual(regs.harness_family_types["good_barding"][0], 1)
        self.assertIsNone(regs.harness_family_types["bad_barding"][0],
                          "a harness with no family_type must register as None, not be omitted")
        self.assertIn("horses.xml", regs.harness_family_types["bad_barding"][1])
        self.assertNotIn("saddle_horse", regs.harness_family_types, "mounts are not harnesses")

    def test_mount_family_types_resolved_through_monster_and_base_monster(self):
        modules = Path(self._tmp.name) / "Modules"
        self._write_harness_fixture(modules)
        regs = ts.build_registries(self.md, modules)
        self.assertEqual(regs.mount_family_types["saddle_horse"], 1)
        # HorseComponent never reads family_type off <Horse>; the monsters XML is
        # the only mount-side authority, and base_monster must be followed.
        self.assertEqual(regs.mount_family_types["taom_war_elephant"], 10)

    def test_conflicting_monster_family_types_resolve_to_none(self):
        # ADOD_Beasts redeclares ids that Native/LOTRLOME also define; engine
        # resolution is load-order dependent, so treat it as unknown rather than
        # guessing and emitting a false mismatch.
        modules = Path(self._tmp.name) / "Modules"
        self._write_harness_fixture(modules)
        regs = ts.build_registries(self.md, modules)
        self.assertIsNone(regs.mount_family_types["odd_beast"])

    def test_harness_registries_empty_without_game_install(self):
        regs = ts.build_registries(self.md, None)
        self.assertEqual(regs.harness_family_types, {})
        self.assertEqual(regs.mount_family_types, {})

    def test_shrunken_body_property_registry_warns(self):
        # A renamed vanilla *_bodyproperties.xml silently shrinks the registry. Full
        # shrinkage would trip the empty-registry guard and skip the check entirely
        # (silent PASS); partial shrinkage floods false positives. Neither is
        # distinguishable from a healthy run without a floor.
        modules = Path(self._tmp.name) / "Modules"
        _write(self.md / "TAOM_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty id="only_one" />\n</BodyProperties>\n')
        regs = ts.build_registries(self.md, modules)
        self.assertTrue(regs.suspect_registries,
                        "a 1-entry body_properties registry with a game install must be flagged")
        self.assertIn("body_properties", " ".join(regs.suspect_registries))

    def test_healthy_registry_is_not_flagged_suspect(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBoxCore" / "ModuleData" / "sandboxcore_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n'
               + "".join(f'  <BodyProperty id="v{i}" />\n' for i in range(120))
               + '</BodyProperties>\n')
        _write(self.md / "TAOM_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty id="fighter_haradrim" />\n</BodyProperties>\n')
        regs = ts.build_registries(self.md, modules)
        self.assertEqual([s for s in regs.suspect_registries if "body_properties" in s], [])

    @staticmethod
    def _suspect(regs, label):
        return [s for s in regs.suspect_registries if s.startswith(f"{label} registry")]

    def test_shrunken_settlements_and_rosters_registries_warn(self):
        # A broken source path empties them, and an empty registry switches its pass off silently.
        modules = Path(self._tmp.name) / "Modules"
        regs = ts.build_registries(self.md, modules)
        self.assertEqual(len(self._suspect(regs, "settlements")), 1)
        self.assertEqual(len(self._suspect(regs, "equipment_rosters")), 1)

    def test_healthy_settlements_and_rosters_registries_are_not_flagged(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "TAOM_Map" / "ModuleData" / "settlements.xml",
               '<Settlements>\n' + "".join(f'  <Settlement id="s{i}" />\n' for i in range(600)) + '</Settlements>\n')
        _write(modules / "SandBoxCore" / "ModuleData" / "sets.xml",
               '<EquipmentRosters>\n' + "".join(f'  <EquipmentRoster id="r{i}" />\n' for i in range(2100))
               + '</EquipmentRosters>\n')
        regs = ts.build_registries(self.md, modules)
        self.assertEqual(self._suspect(regs, "settlements"), [])
        self.assertEqual(self._suspect(regs, "equipment_rosters"), [])

    def test_no_game_install_does_not_flag_registries_suspect(self):
        # Empty-by-design, already reported by the CLI. Flagging it here would cry
        # wolf on every run without the game install.
        regs = ts.build_registries(self.md, None)
        self.assertEqual(regs.suspect_registries, [])

    def test_body_properties_registry_built_from_bodyproperties_files(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBoxCore" / "ModuleData" / "sandboxcore_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty id="fighter_empire" />\n</BodyProperties>\n')
        _write(self.md / "TAOM_bodyproperties.xml",
               '<?xml version="1.0"?>\n<BodyProperties>\n  <BodyProperty\n\t\tid="fighter_haradrim">\n  </BodyProperty>\n</BodyProperties>\n')
        regs = ts.build_registries(self.md, modules)
        # TAOM authors the id on its OWN line after a newline+tabs; an id-on-the-
        # same-line regex silently registers nothing and every lord goes broken.
        self.assertIn("fighter_haradrim", regs.body_properties)
        self.assertIn("fighter_empire", regs.body_properties)
        self.assertNotIn("fighter_umbar", regs.body_properties)

    _CLANS_XML = ('<?xml version="1.0"?>\n<Factions>\n%s</Factions>\n')

    def test_factions_registry_built_from_repo_and_vanilla_clans(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBox" / "ModuleData" / "spclans.xml",
               self._CLANS_XML % '  <Faction\n\t\tid="clan_vanilla_1" name="x" />\n')
        _write(self.md / "characters" / "clans.xml",
               self._CLANS_XML % '  <Faction id="clan_taom_1" name="y" />\n'
                                 '  <!-- <Faction id="clan_ghost" /> -->\n')
        regs = ts.build_registries(self.md, modules)
        self.assertIn("clan_vanilla_1", regs.factions)
        self.assertIn("clan_taom_1", regs.factions)
        self.assertNotIn("clan_ghost", regs.factions)

    def test_factions_registry_empty_without_game_install(self):
        _write(self.md / "characters" / "clans.xml",
               self._CLANS_XML % '  <Faction id="clan_taom_1" name="y" />\n')
        regs = ts.build_registries(self.md, None)
        self.assertEqual(regs.factions, set())

    def test_shrunken_factions_registry_warns(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(self.md / "characters" / "clans.xml",
               self._CLANS_XML % '  <Faction id="clan_taom_1" name="y" />\n')
        regs = ts.build_registries(self.md, modules)
        self.assertIn("factions", " ".join(regs.suspect_registries))

    def test_healthy_factions_registry_is_not_flagged_suspect(self):
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBox" / "ModuleData" / "spclans.xml",
               self._CLANS_XML % "".join(f'  <Faction id="v{i}" name="x" />\n' for i in range(60)))
        _write(self.md / "characters" / "clans.xml",
               self._CLANS_XML % "".join(f'  <Faction id="c{i}" name="y" />\n' for i in range(120)))
        regs = ts.build_registries(self.md, modules)
        self.assertEqual([s for s in regs.suspect_registries if "factions" in s], [])

    def test_missing_vanilla_spclans_is_flagged_even_when_repo_clans_fill_the_total(self):
        # The repo's own clans can reach the total floor alone, so a vanished spclans.xml needs
        # its own floor: every Faction ref to a vanilla clan would otherwise read as broken.
        modules = Path(self._tmp.name) / "Modules"
        _write(self.md / "characters" / "clans.xml",
               self._CLANS_XML % "".join(f'  <Faction id="c{i}" name="y" />\n' for i in range(120)))
        regs = ts.build_registries(self.md, modules)
        self.assertIn("vanilla_factions", " ".join(regs.suspect_registries))

    def test_faction_stripped_by_spclans_xslt_is_not_registered(self):
        # spclans.xslt deletes the five bandit clans from vanilla's file, so a ref to one
        # leaves the settlement or hero ownerless at runtime and must be reported.
        modules = Path(self._tmp.name) / "Modules"
        _write(modules / "SandBox" / "ModuleData" / "spclans.xml",
               self._CLANS_XML % '  <Faction id="clan_vanilla_1" />\n  <Faction id="sea_raiders" />\n'
                                 '  <Faction id="steppe_bandits" />\n')
        _write(self.md / "spclans.xslt", """<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:template match="Faction[@id='sea_raiders']"/>
  <!-- <xsl:template match="Faction[@id='clan_vanilla_1']"/> -->
  <xsl:template match="Faction[@id='steppe_bandits']"></xsl:template>
  <xsl:template match="Faction[@id='clan_vanilla_2']">
    <xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy>
  </xsl:template>
</xsl:stylesheet>
""")
        regs = ts.build_registries(self.md, modules)
        self.assertIn("clan_vanilla_1", regs.factions)
        self.assertNotIn("sea_raiders", regs.factions)
        self.assertNotIn("steppe_bandits", regs.factions)

    def test_commented_definition_not_registered(self):
        _write(self.md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n  <!-- <Culture id="ghost_example" /> -->\n</SPCultures>\n')
        regs = ts.build_registries(self.md, None)
        self.assertIn("gondor", regs.cultures)
        self.assertNotIn("ghost_example", regs.cultures, "a commented-out def must not enter the registry")


class LandlessCultureTests(unittest.TestCase):
    """LANDLESS_CULTURE (crash 099f650c, 2026-08-04). Vanilla SpawnLordParty ends with an unguarded
    Settlement.All.First(x => x.Culture == hero.Culture), so a Lord whose culture owns no settlement
    CTDs the daily clan tick as soon as his faction has no InitialHomeSettlement. TAOM_Map deletes
    every vanilla settlement, so 'every culture owns land' does not hold here."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.registries = ts.Registries(
            items={"None"},
            item_def_files={},
            npccharacters=set(),
            cultures={"gondor", "battania", "looters", "darshi"},
            party_templates=set(),
            settled_cultures={"gondor"},   # battania/looters/darshi own nothing
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _landless(self, registries=None):
        issues = ts.Validator(self.md, self.schemas, registries or self.registries).run()
        return [i for i in issues if i.code == "LANDLESS_CULTURE"]

    def test_lord_in_landless_culture_is_reported(self):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_5_1" occupation="Lord" culture="Culture.battania" />\n'
               '</NPCCharacters>\n')
        found = self._landless()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "lord_5_1")
        self.assertIn("battania", found[0].message)

    def test_lord_in_landed_culture_is_clean(self):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_1_1" occupation="Lord" culture="Culture.gondor" />\n'
               '</NPCCharacters>\n')
        self.assertEqual(self._landless(), [])

    def test_clan_and_kingdom_in_landless_culture_are_reported(self):
        _write(self.md / "characters" / "clans.xml",
               '<?xml version="1.0"?>\n<Factions>\n'
               '  <Faction id="clan_battania_1" culture="Culture.battania" />\n'
               '</Factions>\n')
        _write(self.md / "taom_spkingdoms.xml",
               '<?xml version="1.0"?>\n<Kingdoms>\n'
               '  <Kingdom id="battania" culture="Culture.battania" />\n'
               '</Kingdoms>\n')
        self.assertEqual(
            sorted(i.entry_id for i in self._landless()), ["battania", "clan_battania_1"])

    def test_non_lord_occupation_is_ignored(self):
        # Bandit heroes never reach the throwing line: GetBestAvailableCommander filters on
        # Occupation.Lord.
        _write(self.md / "characters" / "bandits.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="looter_boss" occupation="Bandit" culture="Culture.looters" />\n'
               '  <NPCCharacter id="a_wanderer" occupation="Wanderer" culture="Culture.battania" />\n'
               '</NPCCharacters>\n')
        self.assertEqual(self._landless(), [])

    def test_allowlisted_cultures_are_not_reported(self):
        # darshi/looters are landless by design and listed in _LANDLESS_BY_DESIGN with reasons.
        _write(self.md / "characters" / "minor.xml",
               '<?xml version="1.0"?>\n<Factions>\n'
               '  <Faction id="ghilman" culture="Culture.darshi" />\n'
               '  <Faction id="looters" culture="Culture.looters" />\n'
               '</Factions>\n')
        self.assertEqual(self._landless(), [])

    def test_check_skipped_when_settlement_registry_unavailable(self):
        # Degraded mode (no game install): an empty settled_cultures must SKIP the check, not
        # report every culture in TAOM as landless.
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_5_1" occupation="Lord" culture="Culture.battania" />\n'
               '</NPCCharacters>\n')
        degraded = ts.Registries(
            items={"None"}, item_def_files={}, npccharacters=set(),
            cultures={"battania"}, party_templates=set(), settled_cultures=set())
        self.assertEqual(self._landless(degraded), [])


class SettledCultureRegistryTests(unittest.TestCase):
    """build_settled_cultures must model the world the GAME builds, not the union of every module's
    settlements.xml. TAOM_Map ships `<xsl:template match="Settlement"/>`, which deletes all 494
    vanilla settlements; a registry that ignored the strip would report every vanilla culture as
    landed and print PASS while the game crashed."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"

    def tearDown(self):
        self._tmp.cleanup()

    def _write_module(self, name, cultures, strip=False):
        md = self.modules / name / "ModuleData"
        _write(md / "settlements.xml",
               '<?xml version="1.0"?>\n<Settlements>\n' + "".join(
                   f'  <Settlement id="s_{name}_{i}" culture="Culture.{c}" />\n'
                   for i, c in enumerate(cultures)) + "</Settlements>\n")
        if strip:
            _write(md / "settlements.xslt",
                   '<?xml version="1.0"?>\n'
                   '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
                   '  <xsl:template match="Settlement"/>\n'
                   '</xsl:stylesheet>\n')

    def test_unconditional_strip_discards_earlier_modules(self):
        self._write_module("SandBox", ["battania", "khuzait", "sturgia"])
        self._write_module("TAOM_Map", ["gondor", "mordor"], strip=True)
        settled = ts.build_settled_cultures(self.modules)
        self.assertEqual(settled, {"gondor", "mordor"})
        self.assertNotIn("battania", settled,
                         "vanilla settlements are deleted by TAOM_Map's strip - counting them is "
                         "exactly the bug this registry exists to avoid")

    def test_without_a_strip_modules_merge(self):
        self._write_module("SandBox", ["battania"])
        self._write_module("TAOM_Map", ["gondor"])
        self.assertEqual(ts.build_settled_cultures(self.modules), {"battania", "gondor"})

    def test_no_game_install_yields_empty_registry(self):
        self.assertEqual(ts.build_settled_cultures(None), set())

    def test_registry_is_wired_into_build_registries_and_floored(self):
        md = Path(self._tmp.name) / "ModuleData"
        _write(md / "taom_spcultures.xml",
               '<?xml version="1.0"?>\n<SPCultures>\n  <Culture id="gondor" />\n</SPCultures>\n')
        self._write_module("TAOM_Map", ["gondor", "mordor"], strip=True)
        regs = ts.build_registries(md, self.modules)
        self.assertEqual(regs.settled_cultures, {"gondor", "mordor"})
        # Two entries is far below the floor: a broken source path must say so out loud rather
        # than skipping the check and printing a clean PASS.
        self.assertTrue(any("settled_cultures" in s for s in regs.suspect_registries))


class CreatureGearObtainableTests(unittest.TestCase):
    """CREATURE_GEAR_OBTAINABLE. Troll weapons and armour must never reach a player: the engine keeps
    an item out of shops, workshops, loot and TAOM's tournament pool only through
    is_merchandise="false", and out of the smithy only through is_hidden="true" on its crafting
    pieces. Both live in the unversioned Armory (Troll Mace I and the troll shield shipped as
    merchandise, every troll piece researchable), so the repo needs this gate."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.trade = {
            "troll_club": ts.ItemTrade(False, ("troll_head", "troll_handle"), "w.xml", 10),
            "troll_plate": ts.ItemTrade(False, (), "a.xml", 20),
            "wm_cave_troll_shield_a01": ts.ItemTrade(False, (), "s.xml", 30),
            "human_sword": ts.ItemTrade(True, ("human_blade",), "w.xml", 40),
        }
        self.hidden = {"troll_head": True, "troll_handle": True, "human_blade": False}
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _found(self):
        regs = ts.Registries(
            items=set(self.trade) | {"None"}, item_def_files={}, npccharacters=set(),
            cultures={"mordor"}, party_templates=set(),
            item_trade=self.trade, hidden_pieces=self.hidden)
        issues = ts.Validator(self.md, self.schemas, regs).run()
        return [i for i in issues if i.code == "CREATURE_GEAR_OBTAINABLE"]

    def _write_troop(self, race="cave_troll", body=None, troop_id="cave_troll"):
        body = body if body is not None else (
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <equipment slot="Item0" id="Item.troll_club" />\n'
            '        <equipment slot="Body" id="Item.troll_plate" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n')
        _write(self.md / "troops" / "troops_mordor.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               f'  <NPCCharacter id="{troop_id}" race="{race}" level="51" culture="Culture.mordor">\n'
               f'{body}  </NPCCharacter>\n</NPCCharacters>\n')

    def test_locked_troll_gear_is_clean(self):
        self._write_troop()
        self.assertEqual(self._found(), [])

    def test_troll_item_left_as_merchandise_is_reported(self):
        self.trade["troll_plate"] = ts.ItemTrade(True, (), "a.xml", 20)
        self._write_troop()
        found = self._found()
        self.assertEqual([f.entry_id for f in found], ["troll_plate"])
        self.assertEqual(found[0].severity, ts.Severity.ERROR)
        self.assertIn('is_merchandise="false"', found[0].message)

    def test_unhidden_piece_of_a_troll_weapon_is_reported(self):
        self.hidden["troll_handle"] = False
        self._write_troop()
        found = self._found()
        self.assertEqual([f.entry_id for f in found], ["troll_handle"])
        self.assertIn('is_hidden="true"', found[0].message)

    def test_piece_with_no_definition_is_reported_not_passed(self):
        # A piece no crafting_pieces file defines cannot be shown hidden; reading it as hidden is a silent pass.
        self.trade["troll_club"] = ts.ItemTrade(False, ("troll_head", "troll_ghost"), "w.xml", 10)
        self._write_troop()
        found = self._found()
        self.assertEqual([f.entry_id for f in found], ["troll_ghost"])
        self.assertIn("not defined", found[0].message)

    def test_piece_shared_by_two_troll_weapons_is_reported_once(self):
        self.hidden["troll_head"] = False
        self.trade["troll_club_b"] = ts.ItemTrade(False, ("troll_head",), "w.xml", 50)
        self._write_troop(body=(
            '    <Equipments>\n      <EquipmentRoster>\n'
            '        <equipment slot="Item0" id="Item.troll_club" />\n'
            '        <equipment slot="Item1" id="Item.troll_club_b" />\n'
            '      </EquipmentRoster>\n    </Equipments>\n'))
        self.assertEqual([f.entry_id for f in self._found()], ["troll_head"])

    def test_gear_reached_through_a_named_standalone_roster_is_checked(self):
        self.trade["troll_plate"] = ts.ItemTrade(True, (), "a.xml", 20)
        _write(self.md / "equipmentsets" / "troll_sets.xml",
               '<?xml version="1.0"?>\n<EquipmentRosters>\n'
               '  <EquipmentRoster id="troll_kit">\n    <EquipmentSet>\n'
               '      <Equipment slot="Body" id="Item.troll_plate" />\n'
               '    </EquipmentSet>\n  </EquipmentRoster>\n</EquipmentRosters>\n')
        self._write_troop(body='    <Equipments>\n      <EquipmentSet id="troll_kit" />\n    </Equipments>\n')
        self.assertEqual([f.entry_id for f in self._found()], ["troll_plate"])

    def test_hill_troll_race_is_covered(self):
        self.trade["troll_plate"] = ts.ItemTrade(True, (), "a.xml", 20)
        self._write_troop(race="hill_troll", troop_id="hill_troll")
        self.assertEqual([f.entry_id for f in self._found()], ["troll_plate"])

    def test_unworn_troll_shield_is_checked_by_name(self):
        self.trade["wm_cave_troll_shield_a01"] = ts.ItemTrade(True, (), "s.xml", 30)
        self._write_troop()
        found = self._found()
        self.assertEqual([f.entry_id for f in found], ["wm_cave_troll_shield_a01"])
        self.assertIn("no troop carries", found[0].message)

    def test_non_troll_character_wearing_merchandise_is_not_this_gates_business(self):
        self._write_troop()
        _write(self.md / "troops" / "troops_gondor.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="gondor_soldier" culture="Culture.mordor" level="11">\n'
               '    <Equipments>\n      <EquipmentRoster>\n'
               '        <equipment slot="Item0" id="Item.human_sword" />\n'
               '      </EquipmentRoster>\n    </Equipments>\n'
               '  </NPCCharacter>\n</NPCCharacters>\n')
        self.assertEqual(self._found(), [])

    def test_no_troll_character_at_all_is_a_finding_not_a_clean_run(self):
        self._write_troop(race="orc", troop_id="uruk")
        found = self._found()
        self.assertEqual([f.entry_id for f in found], ["(index)"])
        self.assertIn("checked", found[0].message)

    def test_without_the_install_the_gate_is_skipped(self):
        self.trade, self.hidden = {}, {}
        self._write_troop()
        self.assertEqual(self._found(), [])


class BuildItemTradeTests(unittest.TestCase):
    """build_item_trade reads the merchandise flag the way ItemObject.Deserialize does (absent or
    "true" is merchandise, anything else is not) and only from <Items> documents."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name) / "ModuleData"

    def tearDown(self):
        self._tmp.cleanup()

    def test_flags_pieces_and_document_kinds(self):
        _write(self.root / "items" / "weapons.xml",
               '<?xml version="1.0"?>\n<Items>\n'
               '  <!-- <Item id="commented_out" is_merchandise="true" /> -->\n'
               '  <Item id="no_flag" Type="Shield" />\n'
               '  <Item id="sold" is_merchandise="true" Type="Shield" />\n'
               '  <Item id="kept" is_merchandise="false" Type="Shield" />\n'
               '  <CraftedItem id="club" crafting_template="Mace" is_merchandise="false">\n'
               '    <Pieces>\n      <Piece id="club_head" Type="Blade" />\n'
               '      <Piece id="club_handle" Type="Handle" />\n    </Pieces>\n'
               '  </CraftedItem>\n</Items>\n')
        # A feature table quoting <Item id> rows defines no item (armour_classes.xml's shape).
        _write(self.root / "armour_acquisition" / "armour_classes.xml",
               '<?xml version="1.0"?>\n<ArmourClasses>\n  <Item id="table_row" class="heavy" />\n</ArmourClasses>\n')
        _write(self.root / "crafting_pieces.xml",
               '<?xml version="1.0"?>\n<CraftingPieces>\n'
               '  <CraftingPiece id="club_head" tier="3" is_hidden="true" piece_type="Blade" />\n'
               '  <CraftingPiece id="club_handle" tier="3" piece_type="Handle">\n  </CraftingPiece>\n'
               '</CraftingPieces>\n')

        trade, hidden = ts.build_item_trade([self.root])

        self.assertTrue(trade["no_flag"].merchandise)
        self.assertTrue(trade["sold"].merchandise)
        self.assertFalse(trade["kept"].merchandise)
        self.assertEqual(trade["club"].pieces, ("club_head", "club_handle"))
        self.assertNotIn("commented_out", trade)
        self.assertNotIn("table_row", trade)
        self.assertEqual(trade["sold"].line, 5)
        self.assertEqual(hidden, {"club_head": True, "club_handle": False})


class MountedDwarfTests(unittest.TestCase):
    """MOUNTED_DWARF. Dwarves use a custom, shorter skeleton whose rider bone is misaligned,
    so a mounted dwarf spawns INSIDE the horse mesh. TAOM already strips the mount for dwarf
    tournament participants (Patch46_TournamentDwarfDismount) — this is the data-layer half of
    the same invariant: no dwarf is ever authored as cavalry, and none is handed a mount.

    Both halves are needed. `default_group` and the Horse slot are independent knobs, so a lord
    left as Infantry but given a horse in his roster still spawns mounted."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.registries = ts.Registries(
            items={"None", "good_sword", "saddle_horse", "sumpter_horse",
                   "taom_war_ram_a", "taom_war_ram_b"},
            item_def_files={},
            npccharacters=set(),
            cultures={"erebor", "vlandia"},
            party_templates=set(),
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _mounted(self):
        issues = ts.Validator(self.md, self.schemas, self.registries).run()
        return [i for i in issues if i.code == "MOUNTED_DWARF"]

    def _write_lord(self, group="Infantry", race="dwarf", body=""):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               f'  <NPCCharacter id="lord_E1_1" race="{race}" occupation="Lord" '
               f'culture="Culture.erebor" default_group="{group}">\n{body}'
               '  </NPCCharacter>\n</NPCCharacters>\n')

    # -- rule A: default_group ------------------------------------------- #
    def test_dwarf_cavalry_is_reported(self):
        self._write_lord(group="Cavalry")
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "lord_E1_1")
        self.assertEqual(found[0].severity, ts.Severity.ERROR)
        self.assertIn("Cavalry", found[0].message)

    def test_dwarf_horse_archer_is_reported(self):
        self._write_lord(group="HorseArcher")
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("HorseArcher", found[0].message)

    def test_dwarf_infantry_is_clean(self):
        self._write_lord(group="Infantry")
        self.assertEqual(self._mounted(), [])

    def test_dwarf_ranged_is_clean(self):
        self._write_lord(group="Ranged")
        self.assertEqual(self._mounted(), [])

    # -- rule B: a mount in reachable battle equipment --------------------- #
    def test_dwarf_with_inline_horse_roster_is_reported(self):
        # Troop shape: equipment sits directly inside the NPCCharacter.
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <Equipment slot="Horse" id="Item.saddle_horse" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("saddle_horse", found[0].message)

    def test_dwarf_referencing_a_mounted_equipmentset_is_reported(self):
        # Lord shape: the NPCCharacter names a standalone roster defined elsewhere.
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentSet id="erebor_bat_a" />\n'
            '    </Equipments>\n'))
        _write(self.md / "equipmentsets" / "taom_equipment_sets_erebor.xml",
               '<?xml version="1.0"?>\n<EquipmentRosters>\n'
               '  <EquipmentRoster id="erebor_bat_a" culture="Culture.erebor">\n'
               '    <EquipmentSet>\n'
               '      <Equipment slot="Horse" id="Item.saddle_horse" />\n'
               '    </EquipmentSet>\n'
               '  </EquipmentRoster>\n</EquipmentRosters>\n')
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "lord_E1_1")
        self.assertIn("erebor_bat_a", found[0].message)

    def test_dwarf_referencing_a_footed_equipmentset_is_clean(self):
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentSet id="erebor_bat_a" />\n'
            '    </Equipments>\n'))
        _write(self.md / "equipmentsets" / "taom_equipment_sets_erebor.xml",
               '<?xml version="1.0"?>\n<EquipmentRosters>\n'
               '  <EquipmentRoster id="erebor_bat_a" culture="Culture.erebor">\n'
               '    <EquipmentSet>\n'
               '      <Equipment slot="Item0" id="Item.good_sword" />\n'
               '    </EquipmentSet>\n'
               '  </EquipmentRoster>\n</EquipmentRosters>\n')
        self.assertEqual(self._mounted(), [])

    # -- positive controls: the check must be dwarf-scoped ----------------- #
    def test_non_dwarf_cavalry_with_a_horse_is_clean(self):
        # Guards against a matcher that fires on everything. A broken scan that
        # silently matches nothing is caught by the reported-cases above; this
        # catches the opposite failure.
        self._write_lord(group="Cavalry", race="human", body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <Equipment slot="Horse" id="Item.saddle_horse" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(self._mounted(), [])

    def test_race_attribute_absent_defaults_to_human_and_is_clean(self):
        # Omitting race= means human (the engine default), never dwarf.
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="lord_1_1" occupation="Lord" '
               'culture="Culture.vlandia" default_group="Cavalry" />\n'
               '</NPCCharacters>\n')
        self.assertEqual(self._mounted(), [])

    def test_lowercase_equipment_tag_is_still_caught(self):
        # TAOM ships both <Equipment> and <equipment> spellings. A case-sensitive
        # matcher reads one of them as "no horses at all" — the exact false-clean
        # this check exists to prevent.
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <equipment slot="Horse" id="Item.saddle_horse" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(len(self._mounted()), 1)

    def test_commented_out_horse_is_ignored(self):
        self._write_lord(body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <!-- <Equipment slot="Horse" id="Item.saddle_horse" /> -->\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(self._mounted(), [])

    # -- rule C: the war-ram carve-out (issue #515) ------------------------ #
    # The Dwarven war ram is the one mount dwarves may ride. Every other mount,
    # horses included, stays a hard error, and "dwarf tagged Cavalry with no mount
    # at all" is still caught by test_dwarf_cavalry_is_reported above.
    def _inline(self, *item_ids) -> str:
        rows = "".join('        <Equipment slot="Horse" id="Item.%s" />\n' % i
                       for i in item_ids)
        return ('    <Equipments>\n'
                '      <EquipmentRoster>\n'
                + rows +
                '      </EquipmentRoster>\n'
                '    </Equipments>\n')

    def _write_roster(self, roster_id: str, mount_id: str) -> None:
        _write(self.md / "equipmentsets" / "taom_equipment_sets_erebor.xml",
               '<?xml version="1.0"?>\n<EquipmentRosters>\n'
               '  <EquipmentRoster id="%s" culture="Culture.erebor">\n'
               '    <EquipmentSet>\n'
               '      <Equipment slot="Horse" id="Item.%s" />\n'
               '    </EquipmentSet>\n'
               '  </EquipmentRoster>\n</EquipmentRosters>\n' % (roster_id, mount_id))

    def test_dwarf_cavalry_on_a_war_ram_is_clean(self):
        # A ram rider genuinely IS cavalry, so the group tag must be allowed here.
        self._write_lord(group="Cavalry", body=self._inline("taom_war_ram_a"))
        self.assertEqual(self._mounted(), [])

    def test_dwarf_on_the_second_war_ram_variant_is_clean(self):
        self._write_lord(group="Cavalry", body=self._inline("taom_war_ram_b"))
        self.assertEqual(self._mounted(), [])

    def test_dwarf_infantry_on_a_war_ram_is_clean(self):
        self._write_lord(group="Infantry", body=self._inline("taom_war_ram_a"))
        self.assertEqual(self._mounted(), [])

    def test_dwarf_horse_archer_on_a_war_ram_is_clean(self):
        # _MOUNTED_GROUPS relaxes BOTH Cavalry and HorseArcher for a ram rider, so pin the second
        # one too. The rule's purpose is "do not declare a dwarf mounted WITHOUT a ram"; a ram
        # rider carrying a bow violates that no more than one carrying a spear does. Flagged by
        # two independent reviewers as permitted-but-untested (issue #515), which is exactly the
        # shape that rots into an accidental behaviour change later.
        self._write_lord(group="HorseArcher", body=self._inline("taom_war_ram_a"))
        self.assertEqual(self._mounted(), [])

    def test_dwarf_horse_archer_without_a_war_ram_is_still_reported(self):
        # The negative half: the relaxation must be gated on actually riding a ram, not on the
        # group tag alone.
        self._write_lord(group="HorseArcher")
        found = self._mounted()
        self.assertTrue(found, "a dwarf HorseArcher with no ram must still be reported")

    def test_dwarf_on_a_war_ram_via_a_named_roster_is_clean(self):
        self._write_lord(group="Cavalry", body=(
            '    <Equipments>\n'
            '      <EquipmentSet id="erebor_ram_a" />\n'
            '    </Equipments>\n'))
        self._write_roster("erebor_ram_a", "taom_war_ram_b")
        self.assertEqual(self._mounted(), [])

    def test_dwarf_on_a_sumpter_horse_is_still_reported(self):
        # Regression guard: the carve-out is an id allowlist, not "any mount".
        self._write_lord(body=self._inline("sumpter_horse"))
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("sumpter_horse", found[0].message)

    def test_dwarf_cavalry_with_a_horse_still_reports_both_halves(self):
        # No ram anywhere, so neither the group rule nor the mount rule relaxes.
        self._write_lord(group="Cavalry", body=self._inline("saddle_horse"))
        found = self._mounted()
        self.assertEqual(len(found), 2)
        self.assertTrue(any("Cavalry" in i.message for i in found))
        self.assertTrue(any("saddle_horse" in i.message for i in found))

    def test_a_war_ram_does_not_mask_a_horse_in_the_same_roster(self):
        # The ram is listed FIRST, so a "first mount wins" scan allowlists it and
        # never looks at the horse behind it. That is the point of this case.
        self._write_lord(group="Cavalry",
                         body=self._inline("taom_war_ram_a", "sumpter_horse"))
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("sumpter_horse", found[0].message)
        self.assertNotIn("war_ram", found[0].message)

    def test_a_war_ram_does_not_mask_a_horse_in_a_named_roster(self):
        # Inline ram plus a named roster carrying the horse. An allowlisted inline
        # mount must not stop the named roster from being resolved.
        self._write_lord(group="Cavalry", body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <Equipment slot="Horse" id="Item.taom_war_ram_a" />\n'
            '      </EquipmentRoster>\n'
            '      <EquipmentSet id="erebor_bat_a" />\n'
            '    </Equipments>\n'))
        self._write_roster("erebor_bat_a", "saddle_horse")
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertIn("saddle_horse", found[0].message)

    def test_non_dwarf_on_a_war_ram_is_clean(self):
        # The rule never applied to non-dwarves; the carve-out must not change that.
        self._write_lord(group="Cavalry", race="human",
                         body=self._inline("taom_war_ram_a"))
        self.assertEqual(self._mounted(), [])

    # -- the gate sees the test_riders folder, and the WildMesh set (#794) ------------ #
    def test_dwarf_in_the_test_riders_folder_on_a_horse_is_reported(self):
        # A CustomGame test-rider file in test_riders/ (where the #794 riders lived) must still be read.
        _write(self.md / "test_riders" / "x.xml",
               '<?xml version="1.0"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="taom_test_x_rider" race="dwarf" culture="Culture.erebor" '
               'default_group="Infantry">\n' + self._inline("saddle_horse") +
               '  </NPCCharacter>\n</NPCCharacters>\n')
        found = self._mounted()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].file, "test_riders/x.xml")
        self.assertIn("saddle_horse", found[0].message)

    def test_dwarf_on_a_wildmesh_boar_with_a_ram_barding_is_clean(self):
        self._write_lord(group="Cavalry", body=(
            '    <Equipments>\n'
            '      <EquipmentRoster>\n'
            '        <equipment slot="Horse" id="Item.taom_wildmesh_boar_a" />\n'
            '        <equipment slot="HorseHarness" id="Item.taom_ram_barding_light_a" />\n'
            '      </EquipmentRoster>\n'
            '    </Equipments>\n'))
        self.assertEqual(self._mounted(), [])

    def test_dwarf_on_a_wildmesh_deer_is_reported(self):
        # Only the three dwarf-sized WildMesh mounts are allowlisted; the deer and moose stay refused.
        self._write_lord(group="Cavalry", body=self._inline("taom_wildmesh_deer_a"))
        found = self._mounted()
        self.assertEqual(len(found), 2)
        self.assertTrue(any("taom_wildmesh_deer_a" in i.message for i in found))


class SettlementEconomyRegistryTests(unittest.TestCase):
    """build_settlement_economy (2026-08-14). It feeds SETTLEMENT_ECONOMY_FLOOR, which guards an
    UNVERSIONED live module file, so a parse that silently drops or mis-attributes a settlement
    disables the gate exactly where it is needed."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"

    def tearDown(self):
        self._tmp.cleanup()

    def _write_settlements(self, module: str, body: str, strip: bool = False) -> None:
        _write(self.modules / module / "ModuleData" / "settlements.xml",
               '<?xml version="1.0"?>\n<Settlements>\n' + body + '</Settlements>\n')
        if strip:
            _write(self.modules / module / "ModuleData" / "settlements.xslt",
                   '<?xml version="1.0"?>\n<xsl:stylesheet version="1.0" '
                   'xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
                   '  <xsl:template match="Settlement"/>\n</xsl:stylesheet>\n')

    def test_strip_xslt_discards_earlier_modules(self):
        # The failure this prevents: counting vanilla's deleted settlements, which makes every
        # culture look landed/floored and reports a clean run while the game disagrees.
        self._write_settlements("SandBox",
            '  <Settlement id="van1" culture="Culture.empire">'
            '<Components><Town prosperity="1000"/></Components></Settlement>\n')
        self._write_settlements("TAOM_Map",
            '  <Settlement id="taom1" culture="Culture.goblin">'
            '<Components><Town prosperity="3000"/></Components></Settlement>\n', strip=True)
        recs = ts.build_settlement_economy(self.modules)
        self.assertEqual([r["id"] for r in recs], ["taom1"])

    def test_castle_town_village_and_hideout_classification(self):
        self._write_settlements("TAOM_Map",
            '  <Settlement id="t" culture="Culture.goblin">'
            '<Components><Town prosperity="3000"/></Components></Settlement>\n'
            '  <Settlement id="c" culture="Culture.goblin">'
            '<Components><Town is_castle="true" prosperity="600"/></Components></Settlement>\n'
            '  <Settlement id="v" culture="Culture.goblin">'
            '<Components><Village hearth="300"/></Components></Settlement>\n'
            '  <Settlement id="h" culture="Culture.goblin"><Components/></Settlement>\n')
        recs = {r["id"]: r["kind"] for r in ts.build_settlement_economy(self.modules)}
        self.assertEqual(recs, {"t": "town", "c": "castle", "v": "village"})

    def test_village_record_carries_its_bound_fortification(self):
        # FORTIFICATION_WITHOUT_VILLAGE (#597) reads it; a fortification is a town or castle that no
        # village record names, so a parse that dropped the attribute would call every fief empty.
        self._write_settlements("TAOM_Map",
            '  <Settlement id="castle_EW7" culture="Culture.gondor">'
            '<Components><Town is_castle="true" prosperity="980"/></Components></Settlement>\n'
            '  <Settlement id="castle_village_EW7_1" culture="Culture.gondor">'
            '<Components><Village hearth="603" bound="Settlement.castle_EW7"/></Components></Settlement>\n')
        recs = {r["id"]: r for r in ts.build_settlement_economy(self.modules)}
        self.assertEqual(recs["castle_village_EW7_1"]["bound"], "castle_EW7")
        self.assertIsNone(recs["castle_EW7"]["bound"])

    def test_fortification_record_carries_its_owner(self):
        # SETTLEMENT_ASSIGNMENT_DRIFT reads it; without it every pinned owner reads as reverted.
        self._write_settlements("TAOM_Map",
            '  <Settlement id="castle_K4" owner="Faction.clan_battania_8" culture="Culture.battania">'
            '<Components><Town is_castle="true" prosperity="420"/></Components></Settlement>\n'
            '  <Settlement id="castle_village_K4_1" culture="Culture.battania">'
            '<Components><Village hearth="465" bound="Settlement.castle_K4"/></Components></Settlement>\n')
        recs = {r["id"]: r for r in ts.build_settlement_economy(self.modules)}
        self.assertEqual(recs["castle_K4"]["owner"], "clan_battania_8")
        self.assertIsNone(recs["castle_village_K4_1"]["owner"])

    def test_decimal_prosperity_is_kept(self):
        # Town.Deserialize uses float.Parse, so 4799.5 is legal data. A `(\\d+)` regex missed it
        # entirely and the fief evaded the floor check (Codex, 2026-08-14).
        self._write_settlements("TAOM_Map",
            '  <Settlement id="t" culture="Culture.goblin">'
            '<Components><Town prosperity="4799.5"/></Components></Settlement>\n')
        recs = ts.build_settlement_economy(self.modules)
        self.assertEqual(len(recs), 1)
        self.assertAlmostEqual(recs[0]["value"], 4799.5)

    def test_self_closing_settlement_does_not_steal_the_next_economy_component(self):
        # A block regex anchored on </Settlement> consumed forward past a self-closing element
        # and attached the FOLLOWING settlement's prosperity to the wrong id.
        self._write_settlements("TAOM_Map",
            '  <Settlement id="empty" culture="Culture.goblin" />\n'
            '  <Settlement id="real" culture="Culture.gundabad">'
            '<Components><Town prosperity="4800"/></Components></Settlement>\n')
        recs = {r["id"]: r for r in ts.build_settlement_economy(self.modules)}
        self.assertNotIn("empty", recs)
        self.assertEqual(recs["real"]["culture"], "gundabad")
        self.assertEqual(recs["real"]["value"], 4800)


class FortificationWithoutVillageTests(unittest.TestCase):
    """FORTIFICATION_WITHOUT_VILLAGE (#597). A town or castle no village is bound to starts every
    campaign with no village trade, no villager parties and no rural notables. town_EW10 and
    town_EW11 shipped that way until 2026-09-13, and the only thing that noticed was a hand count.
    The live map is unversioned, so this is the in-repo gate beside the external edit."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    @staticmethod
    def _rec(sid, kind, bound=None):
        return {"id": sid, "culture": "gondor", "kind": kind, "value": 500, "bound": bound}

    def _issues(self, economy, allow=None):
        regs = ts.Registries(
            items={"None"}, item_def_files={}, npccharacters=set(),
            cultures={"gondor"}, party_templates=set(),
            settled_cultures={"gondor"}, settlement_economy=economy)
        validator = ts.Validator(self.md, self.schemas, regs)
        if allow is not None:
            validator._VILLAGELESS_BY_DESIGN = allow
        return [i for i in validator.run() if i.code == "FORTIFICATION_WITHOUT_VILLAGE"]

    def test_fortification_with_no_bound_village_warns(self):
        economy = [self._rec("town_EW10", "town"),
                   self._rec("castle_EW7", "castle"),
                   self._rec("castle_village_EW7_1", "village", "castle_EW7")]
        found = self._issues(economy, allow={})
        self.assertEqual([i.entry_id for i in found], ["town_EW10"])
        self.assertEqual(found[0].severity, ts.Severity.WARNING)

    def test_fortification_with_a_village_is_clean(self):
        economy = [self._rec("town_EW10", "town"),
                   self._rec("village_EW10_1", "village", "town_EW10")]
        self.assertEqual(self._issues(economy, allow={}), [])

    def test_allowlisted_fortification_is_clean(self):
        economy = [self._rec("castle_G4", "castle")]
        self.assertEqual(self._issues(economy, allow={"castle_G4": "no village entities yet"}), [])

    def test_allowlist_entry_that_now_has_villages_is_reported(self):
        # The entry has rotted the other way: the fix landed and nobody removed the exemption, so
        # the gate would stay blind to that fief if its villages were ever rebound away again.
        economy = [self._rec("castle_G4", "castle"),
                   self._rec("castle_village_G4_1", "village", "castle_G4")]
        found = self._issues(economy, allow={"castle_G4": "no village entities yet"})
        self.assertEqual([i.entry_id for i in found], ["castle_G4"])
        self.assertIn("allowlist", found[0].message)

    def test_allowlist_entry_naming_no_fortification_is_reported(self):
        economy = [self._rec("town_EW10", "town"),
                   self._rec("village_EW10_1", "village", "town_EW10")]
        found = self._issues(economy, allow={"castle_nowhere": "typo"})
        self.assertEqual([i.entry_id for i in found], ["castle_nowhere"])

    def test_records_without_a_bound_key_are_tolerated(self):
        # Older fixtures and the economy-floor tests build records without the key.
        economy = [{"id": "town_EW10", "culture": "gondor", "kind": "town", "value": 500},
                   {"id": "village_EW10_1", "culture": "gondor", "kind": "village", "value": 350}]
        found = self._issues(economy, allow={})
        self.assertEqual([i.entry_id for i in found], ["town_EW10"])

    def test_empty_registry_is_silent(self):
        self.assertEqual(self._issues([], allow={}), [])

    def test_shipped_allowlist_carries_a_reason_per_id(self):
        for sid, reason in ts.Validator._VILLAGELESS_BY_DESIGN.items():
            self.assertTrue(sid.startswith(("town_", "castle_")), sid)
            self.assertTrue(isinstance(reason, str) and reason.strip(), sid)


class SettlementEconomyFloorTests(unittest.TestCase):
    """SETTLEMENT_ECONOMY_FLOOR (2026-08-14). The floored values live in the LIVE TAOM_Map module,
    which is unversioned: a reinstall reverts them silently. This check is the only thing in the
    repo that would notice."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _issues(self, economy):
        regs = ts.Registries(
            items={"None"}, item_def_files={}, npccharacters=set(),
            cultures={"goblin"}, party_templates=set(),
            settled_cultures={"goblin"}, settlement_economy=economy)
        return [i for i in ts.Validator(self.md, self.schemas, regs).run()
                if i.code == "SETTLEMENT_ECONOMY_FLOOR"]

    def _spec_cultures(self):
        spec = json.loads((Path(ts.__file__).resolve().parent
                           / "settlement_economy_floor.json").read_text(encoding="utf-8-sig"))
        return spec["cultures"], spec["floor"]

    def _economy_at_floor(self):
        """Every spec culture holding one of each kind, exactly at its floor. The baseline the
        other tests perturb — a partial world would also trip the observed-nothing check."""
        cultures, floor = self._spec_cultures()
        return [{"id": f"{k}_{c}", "culture": c, "kind": k, "value": floor[fk]}
                for c in cultures
                for k, fk in (("town", "town"), ("castle", "castle"), ("village", "hearth"))]

    def test_value_below_floor_is_reported(self):
        economy = self._economy_at_floor()
        target = next(r for r in economy if r["kind"] == "town")
        target["value"] -= 1
        found = self._issues(economy)
        self.assertEqual(len(found), 1, "exactly the one below-floor town must fail the gate")
        self.assertEqual(found[0].entry_id, target["id"])

    def test_value_at_floor_is_clean(self):
        cultures, floor = self._spec_cultures()
        economy = [{"id": s, "culture": c, "kind": k, "value": floor[fk]}
                   for c in cultures
                   for s, k, fk in ((f"town_{c}", "town", "town"),
                                    (f"castle_{c}", "castle", "castle"),
                                    (f"village_{c}", "village", "hearth"))]
        self.assertEqual(self._issues(economy), [])

    def test_culture_in_spec_with_no_settlement_is_reported(self):
        # A retag or a typo'd id leaves the gate covering nothing for that culture, which
        # otherwise reads exactly like a clean run.
        cultures, floor = self._spec_cultures()
        economy = [{"id": f"town_{c}", "culture": c, "kind": "town", "value": floor["town"]}
                   for c in cultures[1:]]
        found = self._issues(economy)
        self.assertEqual([i.entry_id for i in found], [cultures[0]])

    def test_unrelated_culture_below_the_floor_is_ignored(self):
        _, floor = self._spec_cultures()
        cultures, _ = self._spec_cultures()
        economy = [{"id": f"town_{c}", "culture": c, "kind": "town", "value": floor["town"]}
                   for c in cultures]
        economy.append({"id": "town_gondor", "culture": "gondor", "kind": "town", "value": 1})
        self.assertEqual(self._issues(economy), [])

    def test_registry_unavailable_is_silent_not_clean(self):
        # No game install: the CLI already exits 2. This pass must not manufacture findings,
        # and must not claim a pass either — it simply has nothing to say.
        self.assertEqual(self._issues([]), [])

    def test_floor_above_the_writer_cap_does_not_demand_the_impossible(self):
        # The writer clamps a floor to PROSPERITY_CAP/HEARTH_CAP. If the checker did not, a spec
        # above a cap would fail forever because no --apply could ever satisfy it.
        caps = dict(ts.Validator._floor_caps())
        cultures, _ = self._spec_cultures()
        spec_path = Path(ts.__file__).resolve().parent / "settlement_economy_floor.json"
        original = spec_path.read_text(encoding="utf-8-sig")
        try:
            spec = json.loads(original)
            spec["floor"] = {"town": caps["town"] + 1000, "castle": caps["castle"] + 1000,
                             "hearth": caps["hearth"] + 1000}
            spec_path.write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
            economy = [{"id": s, "culture": c, "kind": k, "value": caps[ck]}
                       for c in cultures
                       for s, k, ck in ((f"town_{c}", "town", "town"),
                                        (f"castle_{c}", "castle", "castle"),
                                        (f"village_{c}", "village", "hearth"))]
            self.assertEqual(self._issues(economy), [],
                             "a value AT the writer's cap must satisfy an over-cap spec")
        finally:
            spec_path.write_text(original, encoding="utf-8")


class SettlementAssignmentDriftTests(unittest.TestCase):
    """SETTLEMENT_ASSIGNMENT_DRIFT (2026-10-30). Mike moved castle_K4 to Khand and castle_FH8 to
    Abanissa in the LIVE TAOM_Map settlements.xml, which is unversioned: a reinstall or an editor
    save from an older copy puts the old owner and culture back. tools/settlement_assignments.json
    pins them, and this pass is what notices the revert."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        self.spec = Path(self._tmp.name) / "settlement_assignments.json"
        self._write_spec({"castle_K4": {"owner": "clan_battania_8", "culture": "battania",
                                        "reason": "Khand castle"}})

    def tearDown(self):
        self._tmp.cleanup()

    def _write_spec(self, settlements):
        self.spec.write_text(json.dumps({"settlements": settlements}), encoding="utf-8")

    @staticmethod
    def _rec(sid, kind, culture, owner=None, bound=None):
        return {"id": sid, "culture": culture, "kind": kind, "value": 500, "bound": bound,
                "owner": owner}

    def _world(self, owner="clan_battania_8", culture="battania", village_culture="battania"):
        return [self._rec("castle_K4", "castle", culture, owner=owner),
                self._rec("castle_village_K4_1", "village", village_culture, bound="castle_K4")]

    def _issues(self, economy):
        regs = ts.Registries(
            items={"None"}, item_def_files={}, npccharacters=set(),
            cultures={"battania", "khuzait"}, party_templates=set(),
            settled_cultures={"battania", "khuzait"}, settlement_economy=economy)
        validator = ts.Validator(self.md, self.schemas, regs)
        validator._ASSIGNMENT_SPEC = self.spec
        return [i for i in validator.run() if i.code == "SETTLEMENT_ASSIGNMENT_DRIFT"]

    def test_matching_world_is_clean(self):
        self.assertEqual(self._issues(self._world()), [])

    def test_reverted_owner_is_an_error(self):
        found = self._issues(self._world(owner="clan_khuzait_1"))
        self.assertEqual([i.entry_id for i in found], ["castle_K4"])
        self.assertEqual(found[0].severity, ts.Severity.ERROR)
        self.assertIn("clan_khuzait_1", found[0].message)

    def test_reverted_culture_is_an_error(self):
        found = self._issues(self._world(culture="khuzait"))
        self.assertEqual([i.entry_id for i in found], ["castle_K4"])
        self.assertIn("khuzait", found[0].message)

    def test_bound_village_of_another_culture_is_an_error(self):
        # A village follows its castle's owner but carries its own culture attribute, so it can
        # be reverted alone and spawn the old culture's headmen under the new owner.
        found = self._issues(self._world(village_culture="khuzait"))
        self.assertEqual([i.entry_id for i in found], ["castle_village_K4_1"])

    def test_pinned_id_missing_from_the_world_is_an_error(self):
        self._write_spec({"castle_nowhere": {"owner": "clan_x", "culture": "battania",
                                             "reason": "typo"}})
        found = self._issues(self._world())
        self.assertEqual([i.entry_id for i in found], ["castle_nowhere"])

    def test_missing_spec_is_an_error(self):
        self.spec.unlink()
        found = self._issues(self._world())
        self.assertEqual([i.entry_id for i in found], ["(spec)"])

    def test_spec_with_no_entries_is_an_error(self):
        # An empty spec gates nothing and reads exactly like a clean run.
        self._write_spec({})
        found = self._issues(self._world())
        self.assertEqual([i.entry_id for i in found], ["(spec)"])

    def test_entry_without_owner_culture_or_reason_is_an_error(self):
        self._write_spec({"castle_K4": {"owner": "clan_battania_8", "culture": "battania"}})
        found = self._issues(self._world())
        self.assertEqual([i.entry_id for i in found], ["castle_K4"])
        self.assertIn("reason", found[0].message)

    def test_empty_registry_is_silent(self):
        # No install: the CLI already exits 2, so this pass must not fail every install-less run.
        self.assertEqual(self._issues([]), [])

    def test_shipped_spec_pins_each_entry_with_a_reason(self):
        spec = json.loads((Path(ts.__file__).resolve().parent
                           / "settlement_assignments.json").read_text(encoding="utf-8-sig"))
        entries = spec["settlements"]
        self.assertTrue(entries, "the shipped spec pins nothing")
        for sid, entry in entries.items():
            self.assertTrue(sid.startswith(("town_", "castle_")), sid)
            for key in ("owner", "culture", "reason"):
                self.assertTrue(str(entry.get(key) or "").strip(), f"{sid}: {key}")



class UpgradeArmourRegressionTests(unittest.TestCase):
    """UPGRADE_ARMOUR_REGRESSION: a promotion must not lower the troop's armour total."""

    TROOPS = """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="src" level="16" default_group="Infantry">
    <upgrade_targets><upgrade_target id="NPCCharacter.{target}" /></upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Head" id="Item.helm_heavy" />
        <equipment slot="Body" id="Item.chest_a" />
      </EquipmentRoster>
      <EquipmentRoster civilian="true">
        <equipment slot="Head" id="Item.helm_lord" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
  <NPCCharacter id="{target}" level="21" default_group="Infantry">
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Head" id="Item.{head}" />
        <equipment slot="Body" id="Item.chest_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
</NPCCharacters>
"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        self.armour = {"helm_light": 10, "helm_heavy": 30, "helm_lord": 90, "chest_a": 40}

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, head, target="tgt", armour=None, cultures_xml=None):
        _write(self.md / "troops" / "troops_x.xml",
               self.TROOPS.format(head=head, target=target))
        if cultures_xml:
            _write(self.md / "taom_spcultures.xml", cultures_xml)
        regs = ts.Registries(items=set(self.armour), item_def_files={},
                             npccharacters={"src", target}, cultures=set(), party_templates=set(),
                             item_armour=self.armour if armour is None else armour)
        return [i for i in ts.Validator(self.md, self.schemas, regs).run()
                if i.code == "UPGRADE_ARMOUR_REGRESSION"]

    def test_lower_armour_target_is_a_warning(self):
        issues = self._run("helm_light")
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0].severity, ts.Severity.WARNING)
        self.assertEqual(issues[0].entry_id, "tgt")
        self.assertIn("Head 30->10", issues[0].message)

    def test_equal_or_higher_armour_target_is_clean(self):
        self.assertEqual(self._run("helm_heavy"), [])
        self.assertEqual(self._run("helm_lord"), [])

    def test_civilian_sets_are_not_compared(self):
        # The source's civilian set wears the 90-armour lord helm; ignoring it is what keeps
        # the heavy-helm target clean above.
        self.assertEqual(self._run("helm_heavy"), [])

    def test_militia_to_militia_is_exempt(self):
        cultures = ('<SPCultures><Culture id="c" militia_troop="NPCCharacter.src" '
                    'melee_militia_troop="NPCCharacter.tgt" /></SPCultures>')
        self.assertEqual(self._run("helm_light", cultures_xml=cultures), [])

    def test_bare_chested_by_design_target_is_judged_without_body_and_cape(self):
        bodyless = sorted(ts.Validator._BODYLESS_BY_DESIGN)[0]
        self.assertEqual(self._run("helm_heavy", target=bodyless), [])
        self.assertEqual(len(self._run("helm_light", target=bodyless)), 1)

    def test_check_is_skipped_without_an_armour_registry(self):
        self.assertEqual(self._run("helm_light", armour={}), [])


class CrossCultureArmourInversionTests(unittest.TestCase):
    """CROSS_CULTURE_ARMOUR_INVERSION (#581): a kingdom's tier-t troops must not sit more than
    the margin under what the game's OTHER kingdoms field two tiers lower. Median-based per
    culture-tier cell, never pairwise, so the elves being twice everyone by design and one odd
    capstone do not drown the signal."""

    TROOPS = """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="{prefix}_t2" level="11" default_group="Infantry">
    <Equipments><EquipmentRoster><equipment slot="Body" id="Item.{t2}" /></EquipmentRoster></Equipments>
  </NPCCharacter>
  <NPCCharacter id="{t4_id}" level="21" default_group="Infantry">
    <Equipments><EquipmentRoster><equipment slot="Body" id="Item.{t4}" /></EquipmentRoster></Equipments>
  </NPCCharacter>
</NPCCharacters>
"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        self.armour = {"lo": 40, "mid": 100, "hi": 200}

    def tearDown(self):
        self._tmp.cleanup()

    def _write_culture(self, culture, t2="hi", t4="hi", t4_id=None):
        _write(self.md / "troops" / f"troops_{culture}.xml",
               self.TROOPS.format(prefix=culture, t2=t2, t4=t4,
                                  t4_id=t4_id or f"{culture}_t4"))

    def _run(self, armour=None, folders=None):
        regs = ts.Registries(items=set(self.armour), item_def_files={},
                             npccharacters=set(), cultures=set(), party_templates=set(),
                             item_armour=self.armour if armour is None else armour,
                             item_folder=folders or {})
        return [i for i in ts.Validator(self.md, self.schemas, regs).run()
                if i.code == "CROSS_CULTURE_ARMOUR_INVERSION"]

    def _field(self, weak_t4="lo", weak_id=None):
        # Culture a's tier-4 troop wears `weak_t4`; b, c and d all field a 200-armour tier 2.
        self._write_culture("a", t2="mid", t4=weak_t4, t4_id=weak_id)
        for c in ("b", "c", "d"):
            self._write_culture(c)

    def test_pure_function_flags_a_cell_under_the_field_median_by_more_than_the_margin(self):
        cells = {("a", 4): [40.0], ("b", 2): [200.0], ("c", 2): [200.0], ("d", 2): [200.0]}
        hits = ts.cross_culture_armour_inversions(cells, margin=20, gap=2, min_cultures=3)
        self.assertEqual([(h["culture"], h["tier"], h["shortfall"]) for h in hits],
                         [("a", 4, 160.0)])
        self.assertEqual(hits[0]["field_tier"], 2)
        self.assertEqual(hits[0]["field"], [("b", 200.0), ("c", 200.0), ("d", 200.0)])
        self.assertEqual(ts.cross_culture_armour_inversions(cells, margin=200, gap=2, min_cultures=3), [])

    def test_weak_tier_is_a_warning_with_the_cell_id(self):
        self._field()
        issues = self._run()
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0].severity, ts.Severity.WARNING)
        self.assertEqual(issues[0].entry_id, "a/tier4")
        self.assertIn("median 40", issues[0].message)
        self.assertIn("160", issues[0].message)
        self.assertIn("analyze_kingdom_armour", issues[0].message)

    def test_within_margin_is_clean(self):
        self._field(weak_t4="hi")
        self.assertEqual(self._run(), [])

    def test_needs_at_least_three_other_cultures(self):
        self._field()
        (self.md / "troops" / "troops_d.xml").unlink()
        self.assertEqual(self._run(), [])

    def test_exempt_ids_and_bodyless_are_left_out(self):
        exempt = sorted(ts.Validator._ARMOUR_LADDER_EXEMPT)[0]
        self._field(weak_id=exempt)
        self.assertEqual(self._run(), [])
        bodyless = sorted(ts.Validator._BODYLESS_BY_DESIGN)[0]
        self._field(weak_id=bodyless)
        self.assertEqual(self._run(), [])

    def test_villagers_are_not_a_culture(self):
        self._field(weak_t4="hi")
        _write(self.md / "characters" / "npcs_x.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="villager_x" level="1">\n'
               '    <Equipments><EquipmentRoster><equipment slot="Body" id="Item.lo" />'
               "</EquipmentRoster></Equipments>\n"
               "  </NPCCharacter>\n</NPCCharacters>\n")
        self.assertEqual(self._run(), [])

    def test_check_is_skipped_without_an_armour_registry(self):
        self._field()
        self.assertEqual(self._run(armour={}), [])

    def test_items_are_scaled_to_the_reference_cap_of_their_own_line(self):
        """Kingdoms differ in armour power by design (#583): a low-cap kingdom's raw totals sit
        under a high-cap kingdom's, and that is not a finding. Every ITEM is scaled to the reference
        cap of the line it belongs to (its folder, or the prefix-routed sub-line), never the wearer's
        culture: an Umbar noble in Black Numenorean plate wears that line's cap."""
        import rebalance_armor as ra
        self.assertEqual(ts.item_cap_for("sm_md_num_grvs_elite_a", "mordor"), ra.KINGDOM_CAPS["mordor_numenorean"])
        self.assertEqual(ts.item_cap_for("sk_uruk_mordor_helmet_a", "mordor"), ra.KINGDOM_CAPS["mordor_uruk"])
        self.assertEqual(ts.item_cap_for("sk_md_orc_x", "mordor"), ra.KINGDOM_CAPS["mordor_orc"])
        self.assertEqual(ts.item_cap_for("sk_dg_khml_x", "rhun"), ra.KINGDOM_CAPS["dol_guldur"])
        self.assertEqual(ts.item_cap_for("anything", "gondor"), ra.KINGDOM_CAPS["gondor"])
        self.assertIsNone(ts.item_cap_for("anything", None))       # vanilla or repo item
        self.assertIsNone(ts.item_cap_for("anything", "troll"))    # a folder without a cap
        self.assertAlmostEqual(ts.scale_to_reference_cap(38, 38), ts.REFERENCE_CAP)
        self.assertEqual(ts.scale_to_reference_cap(40, None), 40)
        old = dict(ra.KINGDOM_CAPS)
        try:
            ref = ts.REFERENCE_CAP
            ra.KINGDOM_CAPS["lowcap"] = 38
            ra.KINGDOM_CAPS["men"] = ref
            self.armour["hi2"] = 200
            # a's tier-4 troop wears `lo` (40) from a 38-cap folder: 40 * ref / 38 against a
            # 200 field whose kit is reference-cap: still flagged.
            self._field()
            self.assertEqual(len(self._run(folders={"lo": "lowcap", "hi": "men"})), 1)
            # Item level, not culture level: the weak troop's 200 is reference-cap kit (stays 200)
            # while the field's identical 200 is 38-cap kit (scales to 200 * ref / 38): flagged.
            self._field(weak_t4="hi2")
            hits = self._run(folders={"hi": "lowcap", "hi2": "men"})
            self.assertEqual(len(hits), 1)
            self.assertIn(f"{200 * ref / 38 - 200:.0f} under", hits[0].message)
            # Same kit on the same cap: clear.
            self.assertEqual(self._run(folders={"hi": "men", "hi2": "men"}), [])
        finally:
            ra.KINGDOM_CAPS.clear()
            ra.KINGDOM_CAPS.update(old)
            self.armour.pop("hi2", None)

    def test_the_reference_cap_is_gondors_cap_in_the_curve(self):
        """REFERENCE_CAP is a literal so the validator stays stdlib-only at import; this is the pin
        that stops it drifting when the maintainer moves Gondor's cap (it read 57 for weeks after
        KINGDOM_CAPS['gondor'] became 46, 2026-11-14)."""
        import rebalance_armor as ra
        self.assertEqual(ts.REFERENCE_CAP, ra.KINGDOM_CAPS["gondor"])

    def test_exempt_ids_still_exist_in_the_shipped_troops(self):
        troops = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "troops"
        if not troops.is_dir():
            self.skipTest("troop data not present")
        ids = set()
        for f in troops.glob("troops_*.xml"):
            ids |= set(re.findall(r'<NPCCharacter[^>]*?\sid="([^"]+)"',
                                  f.read_text(encoding="utf-8-sig", errors="ignore")))
        self.assertGreater(len(ids), 100, "the scan is broken, not the allowlist")
        stale = sorted(set(ts.Validator._ARMOUR_LADDER_EXEMPT) - ids)
        self.assertEqual(stale, [], f"exempt troops no longer exist: {stale}")
        for tid, reason in ts.Validator._ARMOUR_LADDER_EXEMPT.items():
            self.assertTrue(reason.strip(), f"{tid} is exempt without a stated reason")

    def test_noble_lines_and_exempt_pairs_still_exist_in_the_shipped_troops(self):
        """A noble id or an exempt (troop, item) pair that no longer matches the data rots
        silently: the troop would lose its noble band, or a renamed item its excuse."""
        troops = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "troops"
        if not troops.is_dir():
            self.skipTest("troop data not present")
        worn = {}
        for f in troops.glob("troops_*.xml"):
            text = f.read_text(encoding="utf-8-sig", errors="ignore")
            for m in re.finditer(r'<NPCCharacter[^>]*?\sid="([^"]+)"(.*?)</NPCCharacter>', text, re.S):
                worn[m.group(1)] = set(re.findall(r'id="Item\.([^"]+)"', m.group(2)))
        self.assertGreater(len(worn), 100, "the scan is broken, not the allowlist")
        nobles = ts.Validator._NOBLE_LINE_TROOPS
        self.assertEqual(len(nobles), len(set(nobles)), "a noble id is listed twice")
        self.assertEqual(sorted(t for t in nobles if t not in worn), [], "noble troops no longer exist")
        self.assertEqual(sorted(set(nobles) & set(ts.Validator._ARMOUR_LADDER_EXEMPT)), [],
                         "a troop is both exempt and noble; exempt wins and the noble band is lost")
        for (tid, item), reason in ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS.items():
            self.assertIn(item, worn.get(tid, set()), f"exempt pair ({tid}, {item}) is not worn")
            self.assertTrue(reason.strip(), f"({tid}, {item}) is exempt without a stated reason")


class EnlistmentKitRankRegressionTests(unittest.TestCase):
    """ENLISTMENT_KIT_RANK_REGRESSION (#1290): an enlistment kit must not total less armour than any kit
    the resolver issued before it, nor carry a weaker weapon in a category one of them holds. A restat of
    the Armory moves the pieces under the shipped kits, so the file is judged on the installed values."""

    ROSTER = ('    <EquipmentRoster id="enlist_{c}_infantry_{rank}" culture="Culture.{c}">\n'
              '        <EquipmentSet>\n'
              '            <Equipment slot="Item0" id="Item.{weapon}" />\n'
              '            <Equipment slot="Body" id="Item.{body}" />\n'
              '        </EquipmentSet>\n'
              '    </EquipmentRoster>\n')
    STATS = {"chest_lo": {"body_armor": 20}, "chest_hi": {"body_armor": 40, "arm_armor": 5}}
    # The generator's KitWeapon fields: category, output, kind, foot_only, couchable.
    WEAPONS = {"sword_lo": ("one handed", 30.0, "OneHandedSword", False, False),
               "sword_mid": ("one handed", 38.0, "OneHandedSword", False, False),
               "sword_hi": ("one handed", 45.0, "OneHandedSword", False, False),
               "spear": ("spear", 20.0, "OneHandedPolearm", False, False), "mystery": None}

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.md = Path(self._tmp.name) / "ModuleData"

    def _file(self, rows, region=None):
        body = "".join(self.ROSTER.format(c=c, rank=r, weapon=w, body=b) for c, r, w, b in rows)
        if region:
            body = f"  <!-- TAOM-NEWCULTURE:{region}:BEGIN -->\n{body}  <!-- TAOM-NEWCULTURE:{region}:END -->\n"
        _write(self.md / "equipmentsets" / "taom_enlistment_equipment.xml",
               f'<?xml version="1.0" encoding="utf-8"?>\n<EquipmentRosters>\n{body}</EquipmentRosters>\n')

    def _validator(self, stats=None):
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(), item_armour_stats=self.STATS if stats is None else stats)
        return ts.Validator(self.md, [], regs)

    def _issues(self, stats=None, weapons=None):
        return self._validator(stats)._enlistment_kit_rank_regressions(
            weapon_info=self._kit_weapons(self.WEAPONS if weapons is None else weapons))

    @staticmethod
    def _kit_weapons(rows):
        import generate_enlistment_rosters as ger
        return {k: (ger.KitWeapon(*v) if v else None) for k, v in rows.items()}

    def test_a_rank_weaker_in_armour_is_a_finding_on_the_upper_roster(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "sword_lo", "chest_lo")])
        issues = self._issues()
        self.assertEqual([(ts.ENLISTMENT_KIT_RANK_SEVERITY, "ENLISTMENT_KIT_RANK_REGRESSION", "enlist_x_infantry_soldier")],
                         [(i.severity, i.code, i.entry_id) for i in issues])
        self.assertIn("armour total 45 -> 20", issues[0].message)
        self.assertIn("enlist_x_infantry_recruit", issues[0].message)
        self.assertEqual(9, issues[0].line)
        self.assertIn("--repair-ranks --culture x", issues[0].message)

    def test_a_weaker_weapon_of_a_shared_category_is_a_finding(self):
        self._file([("x", "recruit", "sword_hi", "chest_lo"), ("x", "soldier", "sword_lo", "chest_lo")])
        self.assertIn("sword_hi 45.0 -> sword_lo 30.0", self._issues()[0].message)

    def test_every_kit_drawn_before_counts_not_only_the_last(self):
        self._file([("x", "recruit", "sword_hi", "chest_lo"), ("x", "soldier", "sword_lo", "chest_lo"),
                    ("x", "veteran", "sword_mid", "chest_lo")])
        issues = {i.entry_id: i.message for i in self._issues()}
        self.assertIn("sword_hi 45.0 -> sword_mid 38.0 against enlist_x_infantry_recruit",
                      issues["enlist_x_infantry_veteran"])

    def test_a_kit_that_only_drops_a_category_is_no_finding(self):
        # The resolver hands every kit into the party inventory and takes none back, so the player still
        # holds the sword of the recruit kit. The repair tool's dry run lists the dropped categories.
        self._file([("x", "recruit", "sword_lo", "chest_lo"), ("x", "soldier", "spear", "chest_hi")])
        self.assertEqual([], self._issues())

    def test_a_finding_names_its_real_reasons_and_not_the_dropped_category(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "spear", "chest_lo")])
        message = self._issues()[0].message
        self.assertIn("armour total 45 -> 20", message)
        self.assertNotIn("drops the", message)

    def test_a_chain_that_holds_is_clean(self):
        self._file([("x", "recruit", "spear", "chest_lo"), ("x", "soldier", "spear", "chest_hi"),
                    ("x", "veteran", "spear", "chest_hi")])
        self.assertEqual([], self._issues())

    def test_a_piece_the_rule_cannot_judge_is_one_stated_warning(self):
        self._file([("x", "recruit", "mystery", "nowhere"), ("x", "soldier", "mystery", "nowhere")])
        issues = self._issues()
        self.assertEqual([("(unresolved)", ts.Severity.WARNING)], [(i.entry_id, i.severity) for i in issues])
        self.assertIn("mystery, nowhere", issues[0].message)

    def test_a_promoted_cultures_finding_sends_the_repair_to_its_source_and_the_promoter(self):
        self._file([("arn", "recruit", "sword_lo", "chest_hi"), ("arn", "soldier", "sword_lo", "chest_lo")], region="arn")
        with mock.patch.object(ts.Validator, "_promoted_source", return_value=("src", False)):
            message = self._issues()[0].message
        self.assertIn("promote_borrowed_cultures.py --only arn --apply", message)
        self.assertIn("--repair-ranks --culture src", message)
        self.assertNotIn("--repair-ranks --culture arn", message)

    def test_a_promoted_finding_with_a_clean_source_chain_names_the_item_map(self):
        self._file([("src", "recruit", "sword_lo", "chest_hi"), ("src", "soldier", "sword_lo", "chest_hi")])
        text = (self.md / "equipmentsets" / "taom_enlistment_equipment.xml").read_text(encoding="utf-8")
        arn = "".join(self.ROSTER.format(c="arn", rank=r, weapon="sword_lo", body=b)
                      for r, b in (("recruit", "chest_hi"), ("soldier", "chest_lo")))
        _write(self.md / "equipmentsets" / "taom_enlistment_equipment.xml", text.replace(
            "</EquipmentRosters>", f"  <!-- TAOM-NEWCULTURE:arn:BEGIN -->\n{arn}  <!-- TAOM-NEWCULTURE:arn:END -->\n"
                                   "</EquipmentRosters>"))
        with mock.patch.object(ts.Validator, "_promoted_source", return_value=("src", True)):
            issues = self._issues()
        self.assertEqual(["enlist_arn_infantry_soldier"], [i.entry_id for i in issues])
        self.assertIn("the source kits hold the rule", issues[0].message)
        self.assertIn("item_map", issues[0].message)
        self.assertIn("Body chest_hi -> chest_lo", issues[0].message)

    def test_a_source_chain_that_only_drops_a_category_holds_the_rule_so_the_item_map_is_named(self):
        # The source soldier kit drops the recruit's sword: information, not a finding. The clone also
        # loses armour, and the message must send the reader to the item_map, not to a source repair.
        self._file([("src", "recruit", "sword_lo", "chest_hi"), ("src", "soldier", "spear", "chest_hi")])
        text = (self.md / "equipmentsets" / "taom_enlistment_equipment.xml").read_text(encoding="utf-8")
        arn = "".join(self.ROSTER.format(c="arn", rank=r, weapon=w, body=b)
                      for r, w, b in (("recruit", "sword_lo", "chest_hi"), ("soldier", "spear", "chest_lo")))
        _write(self.md / "equipmentsets" / "taom_enlistment_equipment.xml", text.replace(
            "</EquipmentRosters>", f"  <!-- TAOM-NEWCULTURE:arn:BEGIN -->\n{arn}  <!-- TAOM-NEWCULTURE:arn:END -->\n"
                                   "</EquipmentRosters>"))
        with mock.patch.object(ts.Validator, "_promoted_source", return_value=("src", True)):
            issues = self._issues()
        self.assertEqual(["enlist_arn_infantry_soldier"], [i.entry_id for i in issues])
        self.assertIn("the source kits hold the rule", issues[0].message)
        self.assertIn("Body chest_hi -> chest_lo", issues[0].message)

    def test_without_the_installs_armour_values_the_pass_is_skipped(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "sword_lo", "chest_lo")])
        self.assertEqual([], self._issues(stats={}))

    def test_a_tree_without_the_kit_file_checks_nothing(self):
        self.assertEqual([], self._issues())

    def test_weapons_that_could_not_be_priced_are_a_stated_warning_and_armour_is_still_judged(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "sword_lo", "chest_lo")])
        with mock.patch.object(ts.Validator, "_kit_weapon_info", return_value=({}, "no lxml")):
            issues = self._validator()._enlistment_kit_rank_regressions()
        self.assertEqual(["(weapons)", "enlist_x_infantry_soldier"], [i.entry_id for i in issues])
        self.assertIn("no lxml", issues[0].message)
        self.assertEqual(ts.Severity.WARNING, issues[0].severity)

    def test_a_generator_that_cannot_be_imported_is_a_stated_warning(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "sword_lo", "chest_lo")])
        with mock.patch.dict(sys.modules, {"generate_enlistment_rosters": None}):
            issues = self._validator()._enlistment_kit_rank_regressions(weapon_info={})
        self.assertEqual([("(tooling)", ts.Severity.WARNING)], [(i.entry_id, i.severity) for i in issues])

    @staticmethod
    def _import_asserting(name):
        """An __import__ for which `name` fails as the generator's module-level assert fails."""
        real = __import__

        def fake(module, *args, **kwargs):
            if module == name:
                raise AssertionError("a module-level assert")
            return real(module, *args, **kwargs)
        return mock.patch("builtins.__import__", side_effect=fake)

    def test_a_generator_whose_import_asserts_is_the_same_stated_warning(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "sword_lo", "chest_lo")])
        with mock.patch.dict(sys.modules), self._import_asserting("generate_enlistment_rosters"):
            sys.modules.pop("generate_enlistment_rosters", None)
            issues = self._validator()._enlistment_kit_rank_regressions(weapon_info={})
        self.assertEqual([("(tooling)", ts.Severity.WARNING)], [(i.entry_id, i.severity) for i in issues])

    def test_kit_weapon_info_reports_an_import_that_asserts(self):
        with mock.patch.dict(sys.modules), self._import_asserting("generate_enlistment_rosters"):
            sys.modules.pop("generate_enlistment_rosters", None)
            info, problem = ts.kit_weapon_info(self.md, None, {})
        self.assertEqual({}, info)
        self.assertIn("cannot be imported", problem)

    def test_the_pass_is_part_of_a_full_run(self):
        self._file([("x", "recruit", "sword_lo", "chest_hi"), ("x", "soldier", "sword_lo", "chest_lo")])
        with mock.patch.object(ts.Validator, "_kit_weapon_info", return_value=(self._kit_weapons(self.WEAPONS), None)):
            codes = [i.code for i in self._validator().run()]
        self.assertIn("ENLISTMENT_KIT_RANK_REGRESSION", codes)


def _live_modules():
    from _gamedir import DEFAULT_GAME_ROOT, game_modules
    return Path(game_modules(DEFAULT_GAME_ROOT))


@unittest.skipUnless((_live_modules() / "LOTRLOME_Armory" / "ModuleData").is_dir(), "needs the live Armory")
class LiveKitWeaponInfoTests(unittest.TestCase):
    """The rank rule's weapon facts on the installed items (#1290): the couch, a pilum, a bastard sword."""

    @classmethod
    def setUpClass(cls):
        md = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData"
        reg = ts.build_registries(md, _live_modules())
        cls.info, cls.problem = ts.kit_weapon_info(md, _live_modules(), reg.launchers, reg.mount_barred_usages)

    def test_the_weapons_were_priced(self):
        self.assertIsNone(self.problem)

    def test_a_pilum_with_a_thrown_usage_is_not_compared(self):
        self.assertNotIn("imperial_throwing_spear_1_t4", self.info)

    def test_a_lance_is_a_couchable_spear_and_a_long_spear_is_not(self):
        self.assertEqual(("spear", True), (self.info["wm_rohan_ws_spear_a03"].category,
                                           self.info["wm_rohan_ws_spear_a03"].couchable))
        self.assertEqual(("spear", False), (self.info["eastern_spear_4_t4"].category,
                                            self.info["eastern_spear_4_t4"].couchable))

    def test_a_bastard_sword_is_one_handed(self):
        self.assertEqual("one handed", self.info["wm_gondor_lamedon_1h_sword_a"].category)


class WornArmourLimitsTests(unittest.TestCase):
    """WORN_ARMOUR_CEILING and WORN_ARMOUR_FLOOR (#1271, 2026-11-14): what a troop's battle sets add
    up to per region, judged against rebalance_armor's limits. A campaign spawn draws every slot from
    its own set (Equipment.GetRandomEquipmentElements re-rolls the set index per slot whenever the
    seed is not -1, and a troop spawn never passes -1), so the reach is per slot, not per set."""

    STATS = {
        "helm40": {"head_armor": 40}, "helm10": {"head_armor": 10},
        "chest40": {"body_armor": 40}, "chest10": {"body_armor": 10}, "bigchest": {"body_armor": 60},
        "cape10": {"body_armor": 10, "arm_armor": 5}, "cape20": {"body_armor": 20, "arm_armor": 5},
        "bigcape": {"body_armor": 30},
    }

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    @staticmethod
    def _roster(**slots):
        rows = "".join(f'<equipment slot="{s}" id="Item.{i}" />' for s, i in slots.items())
        return f"<EquipmentRoster>{rows}</EquipmentRoster>"

    def _write(self, troops, fname="troops/troops_x.xml"):
        """troops: [(id, level, [roster xml, ...])]"""
        body = "".join(
            f'<NPCCharacter id="{tid}" level="{level}" default_group="Infantry">'
            f'<Equipments>{"".join(rosters)}</Equipments></NPCCharacter>'
            for tid, level, rosters in troops)
        _write(self.md / fname, f'<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>{body}</NPCCharacters>')

    def _run(self, troops, stats="default", fname="troops/troops_x.xml"):
        self._write(troops, fname)
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(),
                             item_armour_stats=self.STATS if stats == "default" else stats)
        return [i for i in ts.Validator(self.md, self.schemas, regs).run()
                if i.code in ("WORN_ARMOUR_CEILING", "WORN_ARMOUR_FLOOR")]

    def test_a_helmetless_elite_is_a_floor_warning(self):
        issues = self._run([("elite_a", 31, [self._roster(Body="chest40")])])
        self.assertEqual([(i.code, i.severity, i.entry_id) for i in issues],
                         [("WORN_ARMOUR_FLOOR", ts.Severity.WARNING, "elite_a")])
        self.assertIn("head 0", issues[0].message)
        self.assertIn("30", issues[0].message)
        self.assertIn("elite", issues[0].message)

    def test_a_troop_that_meets_its_floor_is_clean(self):
        self.assertEqual(self._run([("elite_a", 31, [self._roster(Head="helm40", Body="chest40")])]), [])

    def test_the_floor_follows_the_band_of_the_level(self):
        rosters = [self._roster(Head="helm10", Body="chest10")]
        self.assertEqual(self._run([("light_a", 1, rosters), ("medium_a", 16, rosters)]), [])
        heavy = self._run([("heavy_a", 21, rosters)])
        self.assertEqual([i.code for i in heavy], ["WORN_ARMOUR_FLOOR"])
        self.assertIn("head 10", heavy[0].message)
        self.assertIn("body 10", heavy[0].message)

    def test_a_light_troop_has_no_floor(self):
        self.assertEqual(self._run([("light_a", 1, [self._roster(Body="chest10")])]), [])

    def test_a_set_that_adds_past_the_ceiling_is_flagged(self):
        """A WARNING until every contributor has the rewritten Armory: the validator reads the Armory
        installed on each machine, so an ERROR would block the commits of anyone still on the old one.
        The flip step is in armor-balance.md, "The apply sequence"."""
        # chest 60 + cape 30 = body 90, over vanilla's best troop (67) plus 10.
        issues = self._run([("big", 31, [self._roster(Head="helm40", Body="bigchest", Cape="bigcape")])])
        self.assertEqual([(i.code, i.severity, i.entry_id) for i in issues],
                         [("WORN_ARMOUR_CEILING", ts.Severity.WARNING, "big")])
        self.assertIn("body 90", issues[0].message)
        self.assertIn("77", issues[0].message)
        self.assertIn("before item modifiers", issues[0].message)

    def test_the_limits_are_read_from_rebalance_armor_never_copied(self):
        import rebalance_armor as ra
        rosters = [self._roster(Head="helm40", Body="chest40")]
        self.assertEqual(self._run([("elite_a", 31, rosters)]), [])
        with mock.patch.dict(ra.WORN_ARMOUR_CEILING, {"body": 30}):
            self.assertEqual([i.code for i in self._run([("elite_a", 31, rosters)])], ["WORN_ARMOUR_CEILING"])
        with mock.patch.dict(ra.WORN_ARMOUR_FLOOR["elite"], {"body": 50}):
            self.assertEqual([i.code for i in self._run([("elite_a", 31, rosters)])], ["WORN_ARMOUR_FLOOR"])

    def test_every_slot_is_drawn_from_its_own_set_so_the_floor_is_judged_per_slot(self):
        # Body+cape add to 50 in set one and 30 in set two, so every set clears 30. A spawn can
        # take the thin chest (10) of set two and the thin cape (10) of set one: body 20.
        sets = [self._roster(Head="helm40", Body="chest40", Cape="cape10"),
                self._roster(Head="helm40", Body="chest10", Cape="cape20")]
        issues = self._run([("elite_a", 31, sets)])
        self.assertEqual([i.code for i in issues], ["WORN_ARMOUR_FLOOR"])
        self.assertIn("body 20", issues[0].message)

    def test_every_slot_is_drawn_from_its_own_set_so_the_ceiling_is_judged_per_slot(self):
        # One set holds the heavy chest, the other the heavy cape: no set reaches 77, a spawn can.
        sets = [self._roster(Head="helm40", Body="bigchest"), self._roster(Head="helm40", Cape="bigcape")]
        issues = self._run([("big", 31, sets)])
        self.assertIn("WORN_ARMOUR_CEILING", [i.code for i in issues])

    def test_civilian_sets_are_not_battle_sets(self):
        civilian = '<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.bigchest" /></EquipmentRoster>'
        issues = self._run([("elite_a", 31, [self._roster(Head="helm40", Body="chest40"), civilian])])
        self.assertEqual(issues, [])

    def test_a_ladder_exempt_troop_is_left_out(self):
        exempt = "gondor_ithilien_ranger"
        self.assertIn(exempt, ts.Validator._ARMOUR_LADDER_EXEMPT)
        naked = [self._roster(Gloves="cape10")]
        self.assertEqual(self._run([(exempt, 51, naked)]), [])
        self.assertEqual([i.code for i in self._run([("ordinary_elite", 51, naked)])], ["WORN_ARMOUR_FLOOR"])

    def test_a_troll_is_left_out(self):
        self.assertEqual(self._run([("snow_troll_x", 51, [self._roster(Gloves="cape10")])]), [])

    def test_the_creature_markers_are_the_balance_analyzers(self):
        import analyze_troop_balance as atb
        self.assertEqual(ts.Validator._WORN_ARMOUR_CREATURE_MARKERS, atb.CREATURE_MARKERS)

    def test_a_bodyless_troop_keeps_its_head_floor_and_loses_its_body_floor(self):
        bodyless = "urukhai_champion"
        self.assertIn(bodyless, ts.Validator._BODYLESS_BY_DESIGN)
        self.assertEqual(self._run([(bodyless, 41, [self._roster(Head="helm40")])]), [])
        issues = self._run([(bodyless, 41, [self._roster(Gloves="cape10")])])
        self.assertEqual([i.code for i in issues], ["WORN_ARMOUR_FLOOR"])
        self.assertIn("head 0", issues[0].message)
        self.assertNotIn("body 0", issues[0].message)

    def test_only_the_ladder_troop_files_are_judged(self):
        naked = [self._roster(Gloves="cape10")]
        self.assertEqual(self._run([("villager_x", 31, naked)], fname="characters/npcs_x.xml"), [])

    def test_a_troop_with_no_battle_set_is_left_out(self):
        self.assertEqual(self._run([("empty_a", 31, [])]), [])

    def test_without_an_armour_stats_registry_the_pass_is_skipped(self):
        # The caller (validate_moduledata.main) announces the skip; here nothing is faked.
        self.assertEqual(self._run([("elite_a", 31, [self._roster(Body="chest40")])], stats={}), [])

    def test_a_missing_curve_module_is_a_stated_warning_not_a_silent_pass(self):
        with mock.patch.object(ts, "_curve", return_value=None):
            issues = self._run([("elite_a", 31, [self._roster(Body="chest40")])])
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0].severity, ts.Severity.WARNING)
        self.assertEqual(issues[0].entry_id, "(tooling)")
        self.assertIn("checked nothing", issues[0].message)

    def test_the_ceiling_and_its_tooling_failure_share_one_severity_constant(self):
        """The tooling issue stands in for the ceiling check, so it must never be softer than the
        ceiling: once the ceiling is an error, a broken import left as a warning would let the
        commit hook pass with the check not run."""
        self.assertIs(ts.WORN_ARMOUR_CEILING_SEVERITY, ts.Severity.WARNING)
        big = [("big", 31, [self._roster(Head="helm40", Body="bigchest", Cape="bigcape")])]
        with mock.patch.object(ts, "WORN_ARMOUR_CEILING_SEVERITY", ts.Severity.ERROR):
            ceiling = self._run(big)
            with mock.patch.object(ts, "_curve", return_value=None):
                tooling = self._run(big)
        self.assertEqual([(i.code, i.severity) for i in ceiling],
                         [("WORN_ARMOUR_CEILING", ts.Severity.ERROR)])
        self.assertEqual([(i.entry_id, i.severity) for i in tooling], [("(tooling)", ts.Severity.ERROR)])

    def test_the_floor_stays_a_warning_when_the_ceiling_becomes_an_error(self):
        thin = [("elite_a", 31, [self._roster(Body="chest40")])]
        with mock.patch.object(ts, "WORN_ARMOUR_CEILING_SEVERITY", ts.Severity.ERROR):
            issues = self._run(thin)
        self.assertEqual([(i.code, i.severity) for i in issues], [("WORN_ARMOUR_FLOOR", ts.Severity.WARNING)])

    def test_missing_install_announces_the_worn_armour_skip(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "ModuleData").mkdir()
            _code, _out, err = _run_main("--moduledata", str(Path(tmp) / "ModuleData"),
                                         "--game-modules", str(Path(tmp) / "nope"))
        self.assertIn("WORN_ARMOUR_CEILING SKIPPED", err)

    def test_build_item_armour_stats_keeps_the_four_regions_and_the_total_still_sums_them(self):
        item_xml = (
            '<Items><Item id="chest_x" Type="BodyArmor"><ItemComponent>'
            '<Armor body_armor="30" arm_armor="8" leg_armor="2" /></ItemComponent></Item>'
            '<Item id="helm_x" Type="HeadArmor"><ItemComponent><Armor head_armor="12" /></ItemComponent></Item>'
            '<Item id="sword_x" Type="OneHandedWeapon"><ItemComponent /></Item></Items>')
        with tempfile.TemporaryDirectory() as tmp:
            _write(Path(tmp) / "ModuleData" / "items" / "x.xml", item_xml)
            roots = [Path(tmp) / "ModuleData"]
            self.assertEqual(ts.build_item_armour_stats(roots), {
                "chest_x": {"body_armor": 30, "arm_armor": 8, "leg_armor": 2},
                "helm_x": {"head_armor": 12}})
            self.assertEqual(ts._armour_totals(ts.build_item_armour_stats(roots)), {"chest_x": 40, "helm_x": 12})


class LordArmourRangeTests(unittest.TestCase):
    """LORD_ARMOUR_RANGE (#1271 lord kit pass): a hero wears one whole battle set
    (Hero.SetInitialValuesFromCharacter), so a lord's figure on each part (head, body, arm, leg) is
    his worst whole set, held to the culture's best elite-band troop on that part; his best whole
    set over that troop plus fix_lord_kits.CEILING_MARGIN is the ceiling. Both are warnings, one per
    generic lord template (every lord on it wears the same sets), one per lord for a named roster."""

    STATS = {"chest_light": {"body_armor": 6},
             "chest_elite": {"body_armor": 46, "arm_armor": 10, "leg_armor": 12},
             "pauld_elite": {"body_armor": 13}, "helm": {"head_armor": 40},
             "chest_lordly": {"body_armor": 70, "arm_armor": 20, "leg_armor": 30},
             "pauld_small": {"body_armor": 9}}
    ELITE = ('<Equipment slot="Head" id="Item.helm" /><Equipment slot="Body" id="Item.chest_elite" />'
             '<Equipment slot="Cape" id="Item.pauld_elite" />')

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        _write(self.md / "troops" / "troops_x.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>'
               '<NPCCharacter id="x_guard" level="31" culture="Culture.x" default_group="Infantry"><Equipments>'
               '<EquipmentRoster><equipment slot="Head" id="Item.helm" /><equipment slot="Body" id="Item.chest_elite" />'
               '<equipment slot="Cape" id="Item.pauld_elite" /></EquipmentRoster></Equipments></NPCCharacter>'
               '<NPCCharacter id="x_recruit" level="6" culture="Culture.x" default_group="Infantry"><Equipments>'
               '<EquipmentRoster><equipment slot="Body" id="Item.chest_light" /></EquipmentRoster></Equipments>'
               '</NPCCharacter></NPCCharacters>')
        _write(self.md / "equipmentsets" / "taom_equipment_sets_x.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<EquipmentRosters>'
               '<EquipmentRoster id="x_bat_template_medium_a" culture="Culture.x"><EquipmentSet>'
               '<Equipment slot="Body" id="Item.chest_light" /></EquipmentSet></EquipmentRoster>'
               '<EquipmentRoster id="x_bat_template_medium_b" culture="Culture.x"><EquipmentSet>'
               f'{self.ELITE}</EquipmentSet></EquipmentRoster>'
               '<EquipmentRoster id="x_bat_template_medium_c" culture="Culture.x">'
               f'<EquipmentSet>{self.ELITE}</EquipmentSet><EquipmentSet>'
               '<Equipment slot="Head" id="Item.helm" /><Equipment slot="Body" id="Item.chest_lordly" />'
               '<Equipment slot="Cape" id="Item.pauld_elite" /></EquipmentSet></EquipmentRoster>'
               '<EquipmentRoster id="x_bat_template_medium_d" culture="Culture.x"><EquipmentSet>'
               '<Equipment slot="Head" id="Item.helm" /><Equipment slot="Body" id="Item.chest_elite" />'
               '<Equipment slot="Cape" id="Item.pauld_small" /></EquipmentSet></EquipmentRoster>'
               '</EquipmentRosters>')

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, lords, stats="default", fallback=None):
        """lords: [(id, culture, battle template id)]; fallback: the body of
        taom_lord_template_equipment.xml, when the test needs one."""
        body = "".join(
            f'<NPCCharacter id="{lid}" is_hero="true" occupation="Lord" culture="Culture.{c}">'
            f'<Equipments><EquipmentSet id="{t}" /></Equipments></NPCCharacter>' for lid, c, t in lords)
        _write(self.md / "characters" / "lords.xml",
               f'<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>{body}</NPCCharacters>')
        if fallback is not None:
            _write(self.md / "equipmentsets" / "taom_lord_template_equipment.xml",
                   f'<?xml version="1.0" encoding="utf-8"?>\n<EquipmentRosters>{fallback}</EquipmentRosters>')
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(),
                             item_armour_stats=self.STATS if stats == "default" else stats)
        return [i for i in ts.Validator(self.md, self.schemas, regs).run() if i.code == "LORD_ARMOUR_RANGE"]

    def test_a_template_under_the_best_troop_is_one_warning_naming_each_part(self):
        issues = self._run([("lord_x_1", "x", "x_bat_template_medium_a")])
        self.assertEqual([(i.severity, i.entry_id) for i in issues],
                         [(ts.Severity.WARNING, "x_bat_template_medium_a")])
        msg = issues[0].message
        self.assertTrue(issues[0].file.endswith("equipmentsets/taom_equipment_sets_x.xml"), issues[0].file)
        self.assertIn("1 lord (lord_x_1)", msg)
        self.assertTrue(msg.startswith("floor:"), msg)
        for part in ("head 0 < 40", "body 6 < 59", "arm 0 < 10", "leg 0 < 12"):
            self.assertIn(part, msg)
        self.assertIn("x_guard", msg)
        self.assertIn("fix_lord_kits.py", msg)

    def test_a_lord_at_the_best_troop_is_clean(self):
        self.assertEqual(self._run([("lord_x_2", "x", "x_bat_template_medium_b")]), [])

    def test_a_template_whose_best_whole_set_passes_the_ceiling_is_a_ceiling_warning(self):
        issues = self._run([("lord_x_3", "x", "x_bat_template_medium_c")])
        self.assertEqual([(i.severity, i.entry_id) for i in issues],
                         [(ts.Severity.WARNING, "x_bat_template_medium_c")])
        msg = issues[0].message
        self.assertTrue(msg.startswith("ceiling:"), msg)
        for part in ("body 83 > 64", "arm 20 > 15", "leg 30 > 17"):
            self.assertIn(part, msg)
        self.assertNotIn("head 40", msg)

    def test_a_fallback_roster_is_judged_like_a_lord(self):
        fallback = ('<EquipmentRoster id="taom_x_lord_battle_male" culture="Culture.x"><EquipmentSet>'
                    '<Equipment slot="Body" id="Item.chest_light" /></EquipmentSet></EquipmentRoster>')
        issues = self._run([("lord_x_2", "x", "x_bat_template_medium_b")], fallback=fallback)
        self.assertEqual([i.entry_id for i in issues], ["taom_x_lord_battle_male"])
        self.assertTrue(issues[0].file.endswith("taom_lord_template_equipment.xml"))

    def test_a_part_within_ten_percent_of_the_reference_is_not_a_floor_warning(self):
        # Body 46 + 9 = 55 against 59: 4 under, inside the 5.9 tolerance.
        self.assertEqual(self._run([("lord_x_4", "x", "x_bat_template_medium_d")]), [])

    def test_a_culture_with_no_elite_troop_is_judged_against_its_templates_culture(self):
        issues = self._run([("lord_k_1", "khand", "x_bat_template_medium_a")])
        self.assertEqual([i.entry_id for i in issues], ["x_bat_template_medium_a"])
        self.assertIn("judged as x", issues[0].message)
        self.assertIn("khand", issues[0].message)

    def test_every_lord_on_one_template_is_one_row_that_counts_them(self):
        issues = self._run([("lord_x_1", "x", "x_bat_template_medium_a"), ("lord_x_5", "x", "x_bat_template_medium_a"),
                            ("lord_k_1", "khand", "x_bat_template_medium_a")])
        self.assertEqual(sorted(i.entry_id for i in issues), ["x_bat_template_medium_a"] * 2)
        msgs = sorted(i.message for i in issues)
        self.assertTrue(any("2 lords (lord_x_1, lord_x_5)" in m for m in msgs), msgs)
        self.assertTrue(any("1 lord (lord_k_1)" in m for m in msgs), msgs)

    def test_a_lord_with_a_roster_of_his_own_keeps_a_row_of_his_own(self):
        _write(self.md / "characters" / "lords.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>'
               '<NPCCharacter id="lord_x_named" is_hero="true" occupation="Lord" culture="Culture.x"><Equipments>'
               '<EquipmentRoster><equipment slot="Body" id="Item.chest_light" /></EquipmentRoster>'
               '</Equipments></NPCCharacter></NPCCharacters>')
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(), item_armour_stats=self.STATS)
        issues = [i for i in ts.Validator(self.md, self.schemas, regs).run() if i.code == "LORD_ARMOUR_RANGE"]
        self.assertEqual([i.entry_id for i in issues], ["lord_x_named"])
        self.assertTrue(issues[0].file.endswith("characters/lords.xml"))

    def test_without_an_armour_stats_registry_the_pass_is_skipped(self):
        self.assertEqual(self._run([("lord_x_1", "x", "x_bat_template_medium_a")], stats={}), [])

    def test_no_lords_read_is_a_stated_warning_not_a_clean_pass(self):
        issues = self._run([])
        self.assertEqual([(i.severity, i.entry_id) for i in issues], [(ts.Severity.WARNING, "(tooling)")])
        self.assertIn("read no lords", issues[0].message)

    def test_a_lord_file_that_does_not_parse_is_a_stated_warning(self):
        _write(self.md / "characters" / "broken.xml", '<?xml version="1.0"?>\n<NPCCharacters><oops></NPCCharacters>')
        issues = self._run([("lord_x_2", "x", "x_bat_template_medium_b")])
        self.assertEqual([(i.severity, i.entry_id) for i in issues], [(ts.Severity.WARNING, "(tooling)")])
        self.assertTrue(issues[0].file.endswith("characters/broken.xml"), issues[0].file)
        self.assertIn("could not be parsed", issues[0].message)

    def test_a_missing_lord_kit_module_is_a_stated_warning_not_a_silent_pass(self):
        with mock.patch.object(ts, "_lord_kits", return_value=None):
            issues = self._run([("lord_x_1", "x", "x_bat_template_medium_a")])
        self.assertEqual([(i.severity, i.entry_id) for i in issues], [(ts.Severity.WARNING, "(tooling)")])
        self.assertIn("checked nothing", issues[0].message)

    def test_missing_install_announces_the_lord_armour_skip(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "ModuleData").mkdir()
            _code, _out, err = _run_main("--moduledata", str(Path(tmp) / "ModuleData"),
                                         "--game-modules", str(Path(tmp) / "nope"))
        self.assertIn("LORD_ARMOUR_RANGE", err)


class MeleePassRowsTests(unittest.TestCase):
    """The validator's melee pass prices a troop the way tools/restat_melee_blades.py does: the
    craft key (a troop id prefix can pick the row) and the dominant attack of a polearm
    (melee_ladder.craft_key, melee_ladder.dominant_flags). Rows built from t.culture and the raw
    swing/thrust flags judge a Black Numenorean at orc smithing and a spear against a halberd's swing."""

    SPEC = {"craft_troop_prefix": {"mordor_num_": "mordor_numenorean"},
            "craft_column": {"OneHandedPolearm": "polearm", "OneHandedSword": "sword"}}

    @staticmethod
    def _weapon(template, swing, thrust, swing_dps, thrust_dps, blade="blade_a"):
        from types import SimpleNamespace as NS
        return NS(item=NS(template=template), class_template=template, blade_piece=blade,
                  best_dps=max(swing_dps, thrust_dps), swing_damage=swing, thrust_damage=thrust, swing_dps=swing_dps, thrust_dps=thrust_dps)

    @staticmethod
    def _troop(tid, culture, tier, items):
        from types import SimpleNamespace as NS
        return NS(id=tid, culture=culture, tier=tier, items=set(items))

    def _rows(self, troops, priced):
        return ts.melee_pass_rows(troops, priced, self.SPEC)

    def test_a_troop_id_prefix_picks_the_craft_row_in_both_row_kinds(self):
        priced = {"sw": self._weapon("OneHandedSword", 80, 40, 60, 30)}
        kits, carried = self._rows([self._troop("mordor_num_swordsman", "mordor", 5, ["sw"]),
                                    self._troop("mordor_orc_grunt", "mordor", 5, ["sw"])], priced)
        self.assertEqual([k[1] for k in kits], ["mordor_numenorean", "mordor"])
        self.assertEqual([c[-2:] for c in carried], [(5, "mordor_numenorean"), (5, "mordor")])

    def test_a_thrust_dominant_polearm_is_priced_on_its_thrust_alone(self):
        priced = {"spear": self._weapon("OneHandedPolearm", 50, 70, 30, 60),
                  "halberd": self._weapon("OneHandedPolearm", 90, 40, 80, 20),
                  "sword": self._weapon("OneHandedSword", 80, 40, 60, 30)}
        kits, _ = self._rows([self._troop("t_spear", "gondor", 5, ["spear"]),
                              self._troop("t_halberd", "gondor", 5, ["halberd"]),
                              self._troop("t_sword", "gondor", 5, ["sword"])], priced)
        flags = {k[0]: k[-2:] for k in kits}
        self.assertEqual(flags, {"t_spear": (False, True), "t_halberd": (True, True), "t_sword": (True, True)})

    def test_a_troop_with_a_shield_is_priced_on_the_modes_a_shield_allows(self):
        """A spear behind a shield only thrusts (its two handed swing mode is requires_no_shield)."""
        full = self._weapon("OneHandedPolearm", 90, 40, 80, 20)
        held = self._weapon("OneHandedPolearm", 0, 40, 0, 20)
        held.class_template = "Pike"   # the band follows the class of the mode the troop holds
        full.with_shield = lambda: held
        kits, carried = ts.melee_pass_rows(
            [self._troop("t_shield", "gondor", 5, ["spear", "sh"]),
             self._troop("t_bare", "gondor", 5, ["spear"])],
            {"spear": full}, self.SPEC, shields={"sh"})
        rows = {k[0]: (k[4], k[6], k[-2:]) for k in kits}
        self.assertEqual(rows, {"t_shield": (20, "Pike", (False, True)),
                                "t_bare": (80, "OneHandedPolearm", (True, True))})
        self.assertEqual([(c[0], c[2], c[4], c[6]) for c in carried],
                         [("t_shield", "Pike", 0, 0), ("t_bare", "OneHandedPolearm", 90, 80)])

    def test_each_battle_set_is_scored_and_the_weakest_set_decides_the_under_check(self):
        """#1242: the engine draws a spawn's weapons from its sets, so a strong weapon in one set does
        not hide a weak one in the other. Under reads the weakest set, over reads the best."""
        from types import SimpleNamespace as NS
        priced = {"strong": self._weapon("OneHandedSword", 90, 40, 80, 30),
                  "weak": self._weapon("OneHandedSword", 40, 20, 30, 10)}
        t = NS(id="t_two", culture="gondor", tier=5, items={"strong", "weak"},
               battle_sets=(frozenset({"strong"}), frozenset({"weak"})))
        kits, carried = self._rows([t], priced)
        self.assertEqual([(k[3], k[-1]) for k in kits], [("weak", "under"), ("strong", "over")])
        self.assertEqual(sorted(c[1] for c in carried), ["strong", "weak"])
        one = NS(id="t_one", culture="gondor", tier=5, items={"strong", "weak"})   # no sets: one set, as before
        self.assertEqual([k[3] for k in self._rows([one], priced)[0]], ["strong"])
        same = NS(id="t_same", culture="gondor", tier=5, items={"strong"},
                  battle_sets=(frozenset({"strong"}), frozenset({"strong"})))
        self.assertEqual(len(self._rows([same], priced)[0]), 1)

    def test_a_set_with_no_priced_weapon_is_skipped_not_scored_as_zero(self):
        from types import SimpleNamespace as NS
        priced = {"sw": self._weapon("OneHandedSword", 80, 40, 60, 30)}
        t = NS(id="t_a", culture="gondor", tier=5, items={"sw", "stick"},
               battle_sets=(frozenset({"sw"}), frozenset({"stick"})))
        self.assertEqual([k[3] for k in self._rows([t], priced)[0]], ["sw"])

    def test_a_troop_with_no_priced_weapon_has_no_kit_row_and_no_carried_row(self):
        kits, carried = self._rows([self._troop("t_bare", "gondor", 5, ["unknown"])],
                                   {"sw": self._weapon("OneHandedSword", 80, 40, 60, 30)})
        self.assertEqual((kits, carried), ([], []))

    def test_a_tie_on_the_best_weapon_names_the_first_id_as_the_ladder_report_does(self):
        """#1379: the gate and LADDER.md name the same weapon when two are equally strong."""
        priced = {"sw_a": self._weapon("OneHandedSword", 80, 40, 60, 30),
                  "sw_b": self._weapon("OneHandedSword", 80, 40, 60, 30)}
        kits, _ = self._rows([self._troop("t_sword", "gondor", 5, ["sw_b", "sw_a"])], priced)
        self.assertEqual([k[3] for k in kits], ["sw_a"])

    def test_the_carried_tier_changes_the_verdict_on_a_blow_under_the_class_limit(self):
        import melee_ladder as ml
        spec = ml.load_spec()
        tier = 2
        limit = ml.tier_limit("OneHandedSword", tier, "gondor", spec)
        self.assertLess(limit["swing"] + 5, ml.class_limit("OneHandedSword", spec)["swing"])
        row = ("t_sword", "sw", "OneHandedSword", "blade_a", limit["swing"] + 5, 0, 1.0, 0.0)
        self.assertEqual(ml.blow_findings([row], spec), [])
        found = ml.blow_findings([row + (tier, "gondor")], spec)
        self.assertEqual([f.item for f in found], ["sw"])
        self.assertTrue(found[0].tiered)


class MeleeBattleSetsLoadTests(unittest.TestCase):
    """analyze_melee_ladder.load_troops keeps each battle roster apart (#1242) and reads a roster
    as Civilian the way MBEquipmentRoster.InitEquipment does."""

    XML = ('<NPCCharacters><NPCCharacter id="t" level="21" culture="Culture.gondor"><Equipments>'
           '<EquipmentRoster><equipment slot="Item0" id="Item.sw_strong" /><equipment slot="Item1" id="Item.shield" /></EquipmentRoster>'
           '<EquipmentRoster><equipment slot="Item0" id="Item.sw_weak" /></EquipmentRoster>'
           '<EquipmentRoster civilian="true"><equipment slot="Item0" id="Item.civ_sword" /></EquipmentRoster>'
           '<EquipmentRoster equipmentType="Battle" civilian="true"><equipment slot="Item0" id="Item.sw_third" /></EquipmentRoster>'
           '{extra}</Equipments></NPCCharacter></NPCCharacters>')

    def _load(self, extra=""):
        import analyze_melee_ladder as mr
        if mr.LET is None:
            self.skipTest("needs lxml")
        with tempfile.TemporaryDirectory() as tmp:
            _write(Path(tmp) / "troops_x.xml", self.XML.format(extra=extra))
            return mr.load_troops(Path(tmp))[0]

    def test_each_battle_roster_is_its_own_set_and_a_civilian_true_roster_is_not_one(self):
        t = self._load()
        self.assertEqual(set(t.battle_sets), {frozenset({"sw_strong", "shield"}), frozenset({"sw_weak"}),
                                              frozenset({"sw_third"})})
        self.assertNotIn("civ_sword", t.items)

    def test_a_troop_level_override_replaces_its_slot_in_every_set(self):
        t = self._load('<equipment slot="Item0" id="Item.sw_over" />')
        self.assertEqual(set(t.battle_sets), {frozenset({"sw_over", "shield"}), frozenset({"sw_over"})})

    def test_a_troop_without_rosters_has_one_set_of_its_items(self):
        from analyze_melee_ladder import Troop
        t = Troop("x", "x", 1, "c", "f", frozenset({"a"}))
        self.assertEqual(t.battle_sets, (frozenset({"a"}),))


class PlaceholderMeleePassTests(unittest.TestCase):
    """MELEE_BLOW_CEILING and MELEE_LADDER_INVERSION price the taom_ph_ placeholders (#1271, 2026-11-14).
    The Armory catalogue cannot build them from the install: their pieces register through the
    tree's own placeholder_weapons files, so the pass adds them to a copy of the catalogue the way
    tools/generate_placeholder_weapons.py price_new does and judges each against its real carriers.
    The Armory is a hand-built catalogue here, so the tests need neither the install nor its data."""

    PIECES = ('<CraftingPieces><CraftingPiece id="base_blade" piece_type="Blade" length="80" weight="1.0">'
              '<BladeData><Thrust damage_type="Pierce" damage_factor="2.0"/>'
              '<Swing damage_type="Cut" damage_factor="4.0"/></BladeData></CraftingPiece>'
              '<CraftingPiece id="base_handle" piece_type="Handle" length="20" weight="0.3"/></CraftingPieces>')
    PH_BLADE = ('<CraftingPieces><CraftingPiece id="taom_ph_x_t5_blade" piece_type="Blade" length="80" weight="1.0">'
                '<BladeData><Thrust damage_type="Pierce" damage_factor="{thrust}"/>'
                '<Swing damage_type="Cut" damage_factor="{swing}"/></BladeData></CraftingPiece></CraftingPieces>')
    PH_ITEMS = ('<Items><CraftedItem id="taom_ph_x_t5" name="{{=n}}X" crafting_template="OneHandedSword" '
                'culture="Culture.gondor"><Pieces><Piece id="{blade}" Type="Blade" scale_factor="100" />'
                '<Piece id="base_handle" Type="Handle" scale_factor="100" /></Pieces></CraftedItem></Items>')
    PH_DESCS = ('<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">'
                '<xsl:template match="WeaponDescription[@id=\'Desc\']/AvailablePieces"><xsl:copy>'
                '<xsl:apply-templates select="@*|node()"/><AvailablePiece id="taom_ph_x_t5_blade"/>'
                '</xsl:copy></xsl:template></xsl:stylesheet>')
    TROOP = ('<NPCCharacters><NPCCharacter id="x_knight" level="26" culture="Culture.gondor">'
             '<Equipments><EquipmentRoster><equipment slot="Item0" id="Item.{item}" /></EquipmentRoster>'
             '</Equipments></NPCCharacter></NPCCharacters>')

    def setUp(self):
        import analyze_melee_ladder as mr
        import melee_catalogue as mc
        import melee_damage as md
        import xml.etree.ElementTree as ET
        if mr.LET is None:
            self.skipTest("needs lxml")
        self.mc, self.mr = mc, mr
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.md = self.root / "ModuleData"
        self.cat = mc.Catalogue()
        self.cat.templates["OneHandedSword"] = mc.Template(
            "OneHandedSword", "OneHandedWeapon", [("Blade", 0), ("Handle", 1)], ["Desc"])
        self.cat.descriptions["Desc"] = mc.Description(
            "Desc", "OneHandedSword", frozenset({"MeleeWeapon"}), frozenset({"base_blade", "base_handle"}), ("s",))
        self.cat.usage_sets["s"] = (None, frozenset({"swing", "thrust"}))
        for node in ET.fromstring(self.PIECES).iter("CraftingPiece"):
            piece = md.parse_piece(node)
            self.cat.pieces[piece.id] = piece
        donor = mc.Item("donor", "Donor", "gondor", "OneHandedSword",
                        (("base_blade", "Blade", 100), ("base_handle", "Handle", 100)), "armory")
        self.cat.items["donor"] = donor
        self.priced = {"donor": mc.price(self.cat, donor)}

    def _tree(self, swing="4.0", thrust="2.0", carries="taom_ph_x_t5", files=True, blade="taom_ph_x_t5_blade"):
        _write(self.md / "troops" / "troops_x.xml", self.TROOP.format(item=carries))
        if files:
            ph = self.md / "placeholder_weapons"
            _write(ph / "ph_crafting_pieces.xml", self.PH_BLADE.format(swing=swing, thrust=thrust))
            _write(ph / "ph_items.xml", self.PH_ITEMS.format(blade=blade))
            _write(ph / "ph_weapon_descriptions.xslt", self.PH_DESCS)

    def _issues(self):
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set())
        with mock.patch.object(self.mc, "load", return_value=self.cat), \
             mock.patch.object(self.mc, "price_all", return_value=(self.priced, [])):
            return ts.Validator(self.md, [], regs, game_modules=self.root)._melee_ladder_inversions()

    def _blow(self, issues):
        return [i for i in issues if i.code == "MELEE_BLOW_CEILING"]

    def test_a_placeholder_over_its_tier_limit_is_a_blow_finding_that_names_it(self):
        self._tree(swing="12.0")
        found = self._blow(self._issues())
        self.assertEqual([i.entry_id for i in found], ["taom_ph_x_t5"])
        self.assertIn("x_knight", found[0].message)

    def test_the_same_placeholder_inside_its_limits_is_not_a_blow_finding(self):
        self._tree(swing="3.0", thrust="1.5")
        self.assertEqual(self._blow(self._issues()), [])

    def _vanilla_sword(self, swing):
        """A vanilla-style sword priced on the hand-built catalogue, as _vanilla_priced would return it."""
        import xml.etree.ElementTree as ET
        import melee_damage as md
        node = ET.fromstring('<CraftingPiece id="van_blade" piece_type="Blade" length="80" weight="1.0">'
                             '<BladeData><Thrust damage_type="Pierce" damage_factor="2.0"/>'
                             f'<Swing damage_type="Cut" damage_factor="{swing}"/></BladeData></CraftingPiece>')
        piece = md.parse_piece(node)
        self.cat.pieces[piece.id] = piece
        self.cat.descriptions["Desc"] = self.mc.Description(
            "Desc", "OneHandedSword", frozenset({"MeleeWeapon"}),
            frozenset({"base_blade", "base_handle", "van_blade"}), ("s",))
        item = self.mc.Item("van_sword", "Van", "", "OneHandedSword",
                            (("van_blade", "Blade", 100), ("base_handle", "Handle", 100)), "SandBoxCore")
        self.cat.items["van_sword"] = item
        return {"van_sword": self.mc.price(self.cat, item)}

    def _all_findings(self, vanilla, armory=None):
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set())
        with mock.patch.object(self.mc, "load", return_value=self.cat), \
             mock.patch.object(self.mc, "price_all", return_value=(self.priced if armory is None else armory, [])), \
             mock.patch.object(ts, "_vanilla_priced", return_value=vanilla):
            return ts.Validator(self.md, [], regs, game_modules=self.root)._melee_ladder_inversions()

    def _x_knight_findings(self, vanilla):
        return [i for i in self._all_findings(vanilla) if i.entry_id == "x_knight"]

    def test_a_vanilla_weapon_finding_names_a_hand_repair_not_a_fixer(self):
        """No tool restats or swaps a vanilla weapon (fix_melee_ladder and restat_melee_blades price
        only the Armory), so neither finding may send the reader to one."""
        self._tree(carries="van_sword", files=False)
        found = [i for i in self._all_findings(self._vanilla_sword("40.0"))
                 if i.entry_id in ("x_knight", "van_sword")]
        self.assertEqual({i.code for i in found}, {"MELEE_LADDER_INVERSION", "MELEE_BLOW_CEILING"}, found)
        for issue in found:
            self.assertIn("no tool restats", issue.message)
            self.assertNotIn("fix_melee_ladder.py", issue.message)
            self.assertNotIn("restat_melee_blades.py", issue.message)

    def test_vanilla_prices_do_not_hide_an_armory_that_priced_nothing(self):
        self._tree(carries="van_sword", files=False)
        found = self._all_findings(self._vanilla_sword("40.0"), armory={})
        self.assertEqual([i.entry_id for i in found], ["(install)"])
        self.assertIn("checked nothing", found[0].message)

    def test_a_vanilla_weapon_above_the_band_is_judged_like_an_armory_one(self):
        """#1242: troops that carry vanilla weapons sat outside every gate."""
        self._tree(carries="van_sword", files=False)
        found = self._x_knight_findings(self._vanilla_sword("40.0"))
        self.assertEqual({i.code for i in found}, {"MELEE_LADDER_INVERSION"})
        self.assertTrue(any("over-armed" in i.message and "van_sword" in i.message for i in found), found)
        self.assertEqual(self._x_knight_findings({}), [])

    def test_a_placeholder_nobody_carries_is_not_judged(self):
        self._tree(swing="12.0", carries="donor")
        self.assertEqual(self._blow(self._issues()), [])

    def test_a_tree_without_placeholder_files_judges_the_armory_alone(self):
        self._tree(swing="12.0", files=False)
        issues = self._issues()
        self.assertEqual(self._blow(issues), [])
        self.assertNotIn("could not be priced", " ".join(i.message for i in issues))

    def test_a_placeholder_breach_says_to_restat_the_donor_and_regenerate(self):
        """Restatting the placeholder's own blade would be overwritten by the next generator run."""
        self._tree(swing="12.0")
        message = self._blow(self._issues())[0].message
        self.assertIn("restat its donor", message)
        self.assertIn("generate_placeholder_weapons.py", message)
        self.assertNotIn("lowers the blade to the limit", message)

    def test_a_placeholder_that_cannot_be_priced_is_a_stated_warning_not_a_silent_pass(self):
        self._tree(swing="12.0", blade="taom_ph_missing_blade")
        issues = [i for i in self._issues() if i.entry_id == "taom_ph_x_t5"]
        self.assertEqual([(i.code, i.severity) for i in issues],
                         [("MELEE_LADDER_INVERSION", ts.Severity.WARNING)])
        self.assertIn("could not be priced", issues[0].message)
        self.assertIn("taom_ph_missing_blade", issues[0].message)

    def test_a_placeholder_files_description_sheet_is_what_registers_its_blade(self):
        self._tree(swing="12.0")
        (self.md / "placeholder_weapons" / "ph_weapon_descriptions.xslt").unlink()
        issues = [i for i in self._issues() if i.entry_id == "taom_ph_x_t5"]
        self.assertEqual([i.code for i in issues], ["MELEE_LADDER_INVERSION"])
        self.assertIn("no WeaponDescription", issues[0].message)

    def test_pricing_the_placeholders_leaves_the_shared_catalogue_as_it_was(self):
        self._tree(swing="12.0")
        before = (set(self.cat.items), set(self.cat.pieces), self.cat.descriptions["Desc"].available)
        priced, problems = ts.price_placeholders(self.cat, self.md / "placeholder_weapons")
        self.assertEqual((sorted(priced), problems), (["taom_ph_x_t5"], []))
        self.assertEqual(before, (set(self.cat.items), set(self.cat.pieces), self.cat.descriptions["Desc"].available))

    def test_a_malformed_placeholder_file_is_a_problem_not_a_crash(self):
        self._tree()
        _write(self.md / "placeholder_weapons" / "ph_items.xml", "<Items><CraftedItem")
        priced, problems = ts.price_placeholders(self.cat, self.md / "placeholder_weapons")
        self.assertEqual(priced, {})
        self.assertEqual([p[0] for p in problems], ["ph_items.xml"])

    def test_the_file_names_are_the_ones_the_generator_writes(self):
        import generate_placeholder_weapons as gen
        self.assertEqual(ts.PLACEHOLDER_DIR, gen.OUT_DIR.name)
        self.assertEqual((ts.PLACEHOLDER_PIECES, ts.PLACEHOLDER_ITEMS, ts.PLACEHOLDER_DESCRIPTIONS),
                         (gen.PIECES_FILE.name, gen.ITEMS_FILE.name, gen.DESC_XSLT.name))


class ArmourMeshTierLadderTests(unittest.TestCase):
    """ARMOUR_MESH_TIER_LADDER (#609): a troop wears only the artist mesh tiers its level
    allows (rebalance_armor.MESH_TIER_LADDER). Over-dressed (a level-11 snaga in a `_lord_`
    chest) anchors the whole lord line to the light band under the kingdom-cap curve, which is
    how the Gundabad uruk lord chest reached 20 body armour; under-dressed is cosmetic. Both
    warn, with the direction in the message. Needs no install: the tier is in the id."""

    TROOPS = """<?xml version="1.0" encoding="utf-8"?>
<NPCCharacters>
  <NPCCharacter id="{id}" level="{level}" default_group="Infantry">
    <Equipments>
      <EquipmentRoster><equipment slot="Body" id="Item.{body}" /></EquipmentRoster>
      <EquipmentRoster civilian="true"><equipment slot="Body" id="Item.sk_x_chest_lord_z" /></EquipmentRoster>
    </Equipments>
  </NPCCharacter>
</NPCCharacters>
"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, level, body, tid="x_troop", file="troops/troops_x.xml"):
        _write(self.md / file, self.TROOPS.format(id=tid, level=level, body=body))
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set())
        return [i for i in ts.Validator(self.md, self.schemas, regs).run()
                if i.code == "ARMOUR_MESH_TIER_LADDER"]

    def test_over_dressed_recruit_warns_with_the_direction_and_the_repair(self):
        issues = self._run(11, "sk_x_chest_lord_a")
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0].severity, ts.Severity.WARNING)
        self.assertEqual(issues[0].entry_id, "x_troop")
        self.assertIn("over-dressed", issues[0].message)
        self.assertIn("sk_x_chest_lord_a", issues[0].message)
        self.assertIn("light/medium", issues[0].message)
        self.assertIn("fix_armour_mesh_ladder", issues[0].message)

    def test_under_dressed_capstone_warns_as_cosmetic(self):
        issues = self._run(41, "sk_x_chest_med_a")
        self.assertEqual(len(issues), 1)
        self.assertIn("under-dressed", issues[0].message)

    def test_within_the_ladder_civilian_and_untokened_kit_are_clean(self):
        self.assertEqual(self._run(41, "sk_x_chest_lord_a"), [])
        self.assertEqual(self._run(11, "sk_x_chest_med_a"), [])
        self.assertEqual(self._run(6, "sk_x_civ_heavy_coat_a"), [])
        self.assertEqual(self._run(6, "sk_dale_chest_a03"), [])

    def test_the_gate_passes_the_noble_set_and_the_pairs(self):
        old = (ts.Validator._NOBLE_LINE_TROOPS, ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS)
        try:
            ts.Validator._NOBLE_LINE_TROOPS = ("x_noble",)
            ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS = {("x_paired", "sk_x_chest_heavy_a"): "test"}
            self.assertEqual(self._run(21, "sk_x_chest_heavy_a", tid="x_noble"), [])
            self.assertEqual(self._run(21, "sk_x_chest_heavy_a", tid="x_paired"), [])
            self.assertEqual(len(self._run(21, "sk_x_chest_heavy_a", tid="x_regular")), 1)
        finally:
            ts.Validator._NOBLE_LINE_TROOPS, ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS = old

    def test_exempt_troops_and_villagers_are_left_out(self):
        exempt = sorted(ts.Validator._ARMOUR_LADDER_EXEMPT)[0]
        self.assertEqual(self._run(6, "sk_x_chest_lord_a", tid=exempt), [])
        (self.md / "characters").mkdir()
        self.assertEqual(self._run(6, "sk_x_chest_lord_a", tid="villager_x", file="characters/npcs_x.xml"), [])


class TopTierArmourWornLowTests(unittest.TestCase):
    """TOP_TIER_ARMOUR_WORN_LOW (2026-11-17): an Armory piece a troop of level 36+ and a troop of level 30 or
    lower both wear in battle. The restat prices it at the low troop's band, so the top-tier troop wears low
    armour (the Mirkwood palace guard chest at body 8 on a level 51 guard). One warning per piece; the scope
    is rebalance_armor.shared_down_pieces, the rule tools/generate_armour_band_clones.py plans from."""

    NPC = """  <NPCCharacter id="{id}" level="{level}" default_group="Infantry">
    <Equipments>
      <EquipmentRoster><equipment slot="Body" id="Item.{body}" /></EquipmentRoster>
      <EquipmentRoster civilian="true"><equipment slot="Body" id="Item.civ_coat" /></EquipmentRoster>
    </Equipments>
  </NPCCharacter>
"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, troops, folders):
        body = "".join(self.NPC.format(id=t, level=lv, body=b) for t, lv, b in troops)
        _write(self.md / "troops" / "troops_x.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n' + body + "</NPCCharacters>\n")
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(), item_folder=folders)
        return [i for i in ts.Validator(self.md, self.schemas, regs).run() if i.code == "TOP_TIER_ARMOUR_WORN_LOW"]

    def test_a_piece_a_top_tier_and_a_low_troop_wear_warns_once(self):
        issues = self._run([("x_guard", 51, "x_body"), ("x_warden", 41, "x_body"), ("x_militia", 11, "x_body")],
                           {"x_body": "mirkwood"})
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0].severity, ts.Severity.WARNING)
        self.assertEqual(issues[0].entry_id, "x_body")
        self.assertIn("x_militia", issues[0].message)
        self.assertIn("generate_armour_band_clones", issues[0].message)

    def test_a_piece_under_level_36_or_off_the_armory_or_a_clone_is_clean(self):
        self.assertEqual(self._run([("a", 31, "x_body"), ("b", 11, "x_body")], {"x_body": "mirkwood"}), [])
        self.assertEqual(self._run([("a", 41, "x_body"), ("b", 11, "x_body")], {"x_body": None}), [])
        clone = "taom_band_lt_x_body"
        self.assertEqual(self._run([("a", 41, clone), ("b", 11, clone)], {clone: "mirkwood"}), [])

    def test_a_clone_takes_its_source_folder(self):
        items = Path(self._tmp.name) / "items"
        _write(items / "LOTRLOME_items" / "mirkwood" / "body_armors.xml",
               '<Items><Item id="x_body"><ItemComponent><Armor body_armor="8" /></ItemComponent></Item></Items>')
        _write(items / "armour_band_clones" / "armour_band_clones.xml",
               '<Items><Item id="taom_band_lt_x_body"><ItemComponent><Armor body_armor="8" /></ItemComponent>'
               '</Item></Items>')
        folders = ts.build_item_folders([items])
        self.assertEqual(folders["x_body"], "mirkwood")
        self.assertEqual(folders["taom_band_lt_x_body"], "mirkwood")


class ArmourColourClashTests(unittest.TestCase):
    """ARMOUR_COLOUR_CLASH (2026-11-18): a battle set that puts two metals (gold, bronze, silver) side by side. The families
    come from tools/data/armour_colour_families.json through scan_armour_colours.family_of, the lookup the band clone
    tool's upgrade pick uses; a missing or empty table is one (table) warning, never a silent pass."""

    NPC = """  <NPCCharacter id="{id}" level="30" default_group="Infantry">
    <Equipments>
      <EquipmentRoster><equipment slot="Body" id="Item.{body}" /><equipment slot="Cape" id="Item.{cape}" /></EquipmentRoster>
    </Equipments>
  </NPCCharacter>
"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, troops, table):
        import scan_armour_colours as sac
        body = "".join(self.NPC.format(id=t, body=b, cape=c) for t, b, c in troops)
        _write(self.md / "troops" / "troops_x.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n' + body + "</NPCCharacters>\n")
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(), item_folder={})
        with mock.patch.object(sac, "load_table", return_value=table):
            return [i for i in ts.Validator(self.md, self.schemas, regs).run() if i.code == "ARMOUR_COLOUR_CLASH"]

    def test_silver_beside_bronze_warns_in_both_directions(self):
        table = {"chest_silver": "silver", "cape_bronze": "bronze", "chest_bronze": "bronze", "cape_silver": "silver"}
        for body, cape in (("chest_silver", "cape_bronze"), ("chest_bronze", "cape_silver")):
            issues = self._run([("x_guard", body, cape)], table)
            self.assertEqual(len(issues), 1, (body, cape))
            self.assertEqual(issues[0].severity, ts.Severity.WARNING)
            self.assertEqual(issues[0].entry_id, "x_guard")
            self.assertIn(body, issues[0].message)
            self.assertIn(cape, issues[0].message)

    def test_a_pair_that_does_not_clash_is_clean(self):
        table = {"a": "gold", "b": "bronze", "c": "silver", "d": "neutral", "e": "silvergold"}
        for body, cape in (("c", "d"), ("a", "e"), ("c", "c"), ("vanilla_chest", "a"), ("vanilla_chest", "c")):
            self.assertEqual(self._run([("x_guard", body, cape)], table), [], (body, cape))

    def test_gold_beside_bronze_warns(self):
        # Since 2026-11-18 a set is all gold, all silver or all bronze (the maintainer, on the Mirkwood militia).
        issues = self._run([("x_guard", "a", "b")], {"a": "gold", "b": "bronze"})
        self.assertEqual(len(issues), 1)
        self.assertIn("two metals", issues[0].message)

    def test_a_band_clone_reads_its_own_row_else_its_sources(self):
        table = {"chest": "silver", "cape_bronze": "bronze"}
        self.assertEqual(len(self._run([("x_guard", "taom_band_lt_chest", "cape_bronze")], table)), 1)
        own = dict(table, taom_band_lt_chest="neutral")   # a row of its own wins over the source's
        self.assertEqual(self._run([("x_guard", "taom_band_lt_chest", "cape_bronze")], own), [])

    def test_a_missing_or_empty_table_is_one_warning_that_nothing_was_checked(self):
        issues = self._run([("x_guard", "a", "b")], {})
        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0].severity, ts.Severity.WARNING)
        self.assertEqual(issues[0].entry_id, "(table)")
        self.assertIn("checked nothing", issues[0].message)
        self.assertIn("scan_armour_colours.py", issues[0].message)


class BandCloneSourceWornLowTests(unittest.TestCase):
    """BAND_CLONE_SOURCE_WORN_LOW (review 2026-11-17): a set below the elite band that names the source of an armour
    band clone. The restat prices the source at the elite band, so an NPC, a kit or a low troop's civilian set
    that still names it gets elite armour (the Mirkwood recruit enlistment kits from 23 to 90)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, files, sources=("x_body",)):
        import generate_armour_band_clones as gbc
        for rel, text in files.items():
            _write(self.md / rel, text)
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures=set(),
                             party_templates=set(), item_folder={})
        spec = {"clones": {s: ["light"] for s in sources}, "upgrades": {}, "fallback": []}
        with mock.patch.object(gbc, "load_spec", return_value=spec):
            return [i for i in ts.Validator(self.md, self.schemas, regs).run() if i.code == "BAND_CLONE_SOURCE_WORN_LOW"]

    def test_a_low_npc_and_an_unlevelled_kit_warn_and_a_lord_does_not(self):
        npcs = ('<NPCCharacters>\n'
                '  <NPCCharacter id="guard_x" level="16" occupation="Soldier"><Equipments><EquipmentRoster>'
                '<equipment slot="Body" id="Item.x_body" /></EquipmentRoster></Equipments></NPCCharacter>\n'
                '  <NPCCharacter id="lord_x" occupation="Lord" is_hero="true"><Equipments><EquipmentRoster>'
                '<equipment slot="Body" id="Item.x_body" /></EquipmentRoster></Equipments></NPCCharacter>\n'
                '</NPCCharacters>\n')
        kits = ('<EquipmentRosters>\n  <EquipmentRoster id="mother_x"><EquipmentSet>'
                '<Equipment slot="Body" id="Item.x_body" /></EquipmentSet></EquipmentRoster>\n</EquipmentRosters>\n')
        issues = self._run({"characters/npcs_x.xml": npcs, "equipmentsets/taom_char_creation_equipment.xml": kits,
                            "equipmentsets/taom_lord_template_equipment.xml": kits})
        self.assertEqual(sorted((i.file.replace("\\", "/").split("ModuleData/")[-1], i.entry_id) for i in issues),
                         [("characters/npcs_x.xml", "guard_x"), ("equipmentsets/taom_char_creation_equipment.xml",
                                                                  "mother_x")])
        self.assertTrue(all(i.severity == ts.Severity.WARNING for i in issues))
        self.assertIn("x_body", issues[0].message)
        self.assertIn("generate_armour_band_clones", issues[0].message)

    def test_no_clone_source_is_clean(self):
        npcs = ('<NPCCharacters><NPCCharacter id="guard_x" level="16"><Equipments><EquipmentRoster>'
                '<equipment slot="Body" id="Item.x_body" /></EquipmentRoster></Equipments></NPCCharacter>'
                '</NPCCharacters>\n')
        self.assertEqual(self._run({"characters/npcs_x.xml": npcs}, sources=()), [])


class ExtraRefRootTests(unittest.TestCase):
    """Which foreign modules the CLI sweeps for dangling refs.

    Both live modules are unversioned and outside git, so nothing else gates them:
    `.claude/hooks/check-moduledata-validation.sh` matches staged
    `Main/_Module/ModuleData/*.xml`, which neither module can ever produce. The
    sweep is the only check they get, so the root list is a contract, not a detail.
    Issue #462.
    """

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"
        for name in ("LOTRLOME_Armory", "TAOM_Map"):
            (self.modules / name / "ModuleData").mkdir(parents=True)

    def tearDown(self):
        self._tmp.cleanup()

    def test_armory_is_swept(self):
        roots = vm.build_extra_ref_roots(self.modules)
        self.assertIn(self.modules / "LOTRLOME_Armory" / "ModuleData", roots)

    def test_taom_map_is_swept(self):
        """TAOM_Map carries ~1,012 Culture. refs AND is the sole source of
        settled_cultures, so an unchecked bad id there corrupts LANDLESS_CULTURE
        (the #374 CTD guard) with no diagnostic."""
        roots = vm.build_extra_ref_roots(self.modules)
        self.assertIn(self.modules / "TAOM_Map" / "ModuleData", roots)

    def test_no_roots_without_a_game_install(self):
        roots = vm.build_extra_ref_roots(self.modules.parent / "does_not_exist")
        self.assertEqual(roots, [])

    def test_bad_culture_id_in_taom_map_is_reported(self):
        """End-to-end: the reason the root list matters."""
        md = Path(self._tmp.name) / "ModuleData"
        md.mkdir(parents=True)
        _write(md / "empty.xml", "<NPCCharacters />")
        _write(self.modules / "TAOM_Map" / "ModuleData" / "settlements.xml",
               '<Settlements><Settlement id="s1" culture="Culture.not_a_culture" /></Settlements>')
        registries = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                                   cultures={"mordor"}, party_templates=set(),
                                   body_properties=set())
        issues = ts.Validator(md, [], registries,
                              extra_ref_roots=vm.build_extra_ref_roots(self.modules)).run()
        self.assertTrue(any(i.code == "UNKNOWN_CULTURE" and "not_a_culture" in i.message
                            for i in issues),
                        f"expected UNKNOWN_CULTURE for the TAOM_Map ref, got {[i.code for i in issues]}")

    def test_main_warns_about_a_missing_root_instead_of_crashing(self):
        """A Modules folder missing one of the live modules must print the warning that says
        its sweep was skipped. Validator.missing_ref_roots once held strings while main()
        called `.parent` on them, so this crashed with AttributeError instead."""
        md = Path(self._tmp.name) / "ModuleData"
        md.mkdir(parents=True)
        (self.modules / "TAOM_Map" / "ModuleData").rmdir()
        with mock.patch.object(vm, "generator_item_ref_issues", return_value=[]):
            _, _, err = _run_main("--moduledata", str(md), "--game-modules", str(self.modules),
                                  "--code", "UNKNOWN_CULTURE")
        self.assertIn("extra ref root NOT FOUND, sweep SKIPPED for TAOM_Map", err)


class ArmoryStructuralAssumptionTests(unittest.TestCase):
    """The Armory's schema checks silently do not run, and that is only safe today.

    `item_roots` includes LOTRLOME_Armory, so its items and `<Monster>` decls reach
    the registries, but every schema pass iterates `Validator._xml_files()`, which is
    repo-only. Duplicate-id, enum, civilian `equipmentType`, harness pairing and
    MOUNTED_DWARF therefore never run against the Armory.

    That costs nothing purely because of what the Armory currently contains: items
    and monsters, nothing else. the troop-tree workflow makes it plausible somebody
    authors a troop or a roster there, at which point those checks no-op in silence.
    This test makes that assumption fail loudly instead. Issue #462.
    """

    #: Element types whose checks are repo-only. If the Armory starts defining one,
    #: either extend the schema passes to the extra roots or drop it from this list
    #: with a reason.
    UNCHECKED_ELEMENTS = ("NPCCharacter", "EquipmentRoster", "EquipmentSet",
                          "MBPartyTemplate", "PartyTemplate")

    def test_armory_still_defines_none_of_the_unchecked_element_types(self):
        game_dir = os.environ.get("BANNERLORD_GAME_DIR") or \
            r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
        root = Path(game_dir) / "Modules" / "LOTRLOME_Armory" / "ModuleData"
        if not root.exists():
            self.skipTest(f"no Armory install at {root}")

        offenders = {}
        for xml in root.rglob("*.xml"):
            try:
                text = xml.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for element in self.UNCHECKED_ELEMENTS:
                if f"<{element} " in text or f"<{element}>" in text:
                    offenders.setdefault(element, []).append(xml.name)

        self.assertEqual(
            offenders, {},
            "LOTRLOME_Armory now defines element types whose duplicate-id / enum / "
            "civilian-equipmentType / MOUNTED_DWARF checks are repo-only, so they are "
            "silently NOT running against it. Extend the schema passes to the extra "
            f"ref roots, or update UNCHECKED_ELEMENTS with a reason. Found: {offenders}")


class ArmourSlotCoverageTests(unittest.TestCase):
    """MISSING_BODY_ARMOUR / INCONSISTENT_ARMOUR_SLOT.

    Every other gate asks "does this reference resolve". A troop wearing nothing
    has no reference to resolve and no mesh to look up, so it passes every one of
    them. That is how 15 of 16 Umbar troops shipped in peasant rags with no head,
    cape or gloves on 2026-09-01 with a fully green board.

    The cross-set check exists because the engine draws each slot from an
    INDEPENDENTLY chosen equipment set (`.claude/rules/troops.md`), so a slot
    filled in one battle set and empty in another ships a combination nobody
    authored -- and every UI surface renders set #1, so it looks correct.
    """

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.registries = ts.Registries(
            items={"None", "good_sword", "good_helm", "good_mail", "good_cape"},
            item_def_files={},
            npccharacters=set(),
            cultures={"umbar", "gondor"},
            party_templates=set(),
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _write_troop(self, troop_id: str, rosters: str) -> None:
        _write(self.md / "troops" / "troops_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               f'  <NPCCharacter id="{troop_id}" default_group="Infantry" culture="Culture.umbar">\n'
               f"    <Equipments>{rosters}</Equipments>\n"
               "  </NPCCharacter>\n</NPCCharacters>\n")

    def _codes(self, code):
        issues = ts.Validator(self.md, self.schemas, self.registries).run()
        return [i for i in issues if i.code == code]

    _FULL = ('<EquipmentRoster><equipment slot="Body" id="Item.good_mail" />'
             '<equipment slot="Head" id="Item.good_helm" /></EquipmentRoster>')

    def test_troop_with_no_body_item_is_an_error(self):
        self._write_troop("umbar_naked",
                          '<EquipmentRoster><equipment slot="Head" id="Item.good_helm" />'
                          "</EquipmentRoster>")
        found = self._codes("MISSING_BODY_ARMOUR")
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "umbar_naked")

    def test_troop_with_a_body_item_is_clean(self):
        self._write_troop("umbar_dressed", self._FULL)
        self.assertEqual(self._codes("MISSING_BODY_ARMOUR"), [])

    def test_bodyless_by_design_troops_are_allowlisted(self):
        """Every troop on _BODYLESS_BY_DESIGN is bare-chested on purpose. Removing
        an id from _BODYLESS_BY_DESIGN is what makes this check start guarding it."""
        self._write_troop("urukhai_berserker",
                          '<EquipmentRoster><equipment slot="Leg" id="Item.good_mail" />'
                          "</EquipmentRoster>")
        self.assertEqual(self._codes("MISSING_BODY_ARMOUR"), [])

    def test_a_civilian_roster_does_not_satisfy_the_body_requirement(self):
        """Battle and civilian pools never cross, so a dressed civilian set
        cannot excuse an undressed battle set."""
        self._write_troop("umbar_civ_only",
                          '<EquipmentRoster civilian="true">'
                          '<equipment slot="Body" id="Item.good_mail" /></EquipmentRoster>'
                          '<EquipmentRoster><equipment slot="Head" id="Item.good_helm" />'
                          "</EquipmentRoster>")
        self.assertEqual(len(self._codes("MISSING_BODY_ARMOUR")), 1)

    def test_slot_filled_in_some_battle_sets_but_not_others_warns(self):
        self._write_troop("umbar_mixed",
                          self._FULL
                          + '<EquipmentRoster><equipment slot="Body" id="Item.good_mail" />'
                            '<equipment slot="Head" id="Item.good_helm" />'
                            '<equipment slot="Cape" id="Item.good_cape" /></EquipmentRoster>')
        found = self._codes("INCONSISTENT_ARMOUR_SLOT")
        self.assertEqual(len(found), 1)
        self.assertIn("Cape", found[0].message)
        self.assertIs(found[0].severity, ts.Severity.WARNING)

    def test_identical_battle_sets_do_not_warn(self):
        self._write_troop("umbar_consistent", self._FULL + self._FULL)
        self.assertEqual(self._codes("INCONSISTENT_ARMOUR_SLOT"), [])

    def test_a_self_closing_roster_does_not_swallow_the_next_one(self):
        """`<EquipmentRoster />` appears 326 times in vanilla. A regex that
        matches its own ">" then runs forward and eats the NEXT roster's close
        tag, which BOTH hides a real defect and invents an error on a dressed
        troop. Here the dressed set must survive, so this is clean."""
        self._write_troop("umbar_selfclose",
                          '<EquipmentRoster civilian="true" />' + self._FULL)
        self.assertEqual(self._codes("MISSING_BODY_ARMOUR"), [])

    def test_a_self_closing_battle_roster_is_an_empty_set_not_a_skip(self):
        """The other direction: a self-closing BATTLE roster is a set with
        nothing in it, which is the defect, not something to pass over."""
        self._write_troop("umbar_empty_set", "<EquipmentRoster />")
        self.assertEqual(len(self._codes("MISSING_BODY_ARMOUR")), 1)

    def test_civilian_false_is_a_battle_set(self):
        """A substring test for "civilian" reads civilian="false" AS civilian and
        drops a real battle set from the comparison."""
        self._write_troop("umbar_civ_false",
                          '<EquipmentRoster civilian="false">'
                          '<equipment slot="Head" id="Item.good_helm" /></EquipmentRoster>')
        self.assertEqual(len(self._codes("MISSING_BODY_ARMOUR")), 1)

    def test_an_id_containing_the_word_civilian_is_not_a_civilian_roster(self):
        self._write_troop("umbar_named",
                          '<EquipmentRoster id="umbar_civilian_guard_battle">'
                          '<equipment slot="Head" id="Item.good_helm" /></EquipmentRoster>')
        self.assertEqual(len(self._codes("MISSING_BODY_ARMOUR")), 1)

    def test_single_quoted_slots_are_read(self):
        """slot='Body' is legal XML. A double-quote-only matcher reads the set as
        empty and reports a correctly dressed troop as naked."""
        self._write_troop("umbar_single_quotes",
                          "<EquipmentRoster><equipment slot='Body' id='Item.good_mail' />"
                          "</EquipmentRoster>")
        self.assertEqual(self._codes("MISSING_BODY_ARMOUR"), [])

    def test_missing_body_armour_is_an_error_not_a_warning(self):
        """Severity is load-bearing: the commit hook filters on ERROR codes, so a
        downgrade to WARNING silently unwires the gate."""
        self._write_troop("umbar_naked2",
                          '<EquipmentRoster><equipment slot="Head" id="Item.good_helm" />'
                          "</EquipmentRoster>")
        found = self._codes("MISSING_BODY_ARMOUR")
        self.assertEqual(len(found), 1)
        self.assertIs(found[0].severity, ts.Severity.ERROR)

    def test_every_armour_slot_is_watched(self):
        """Pins _ARMOUR_SLOTS membership. Body and Leg produce no findings on the
        real tree, so dropping either from the tuple is otherwise invisible."""
        self.assertEqual(set(ts.Validator._ARMOUR_SLOTS),
                         {"Head", "Body", "Cape", "Gloves", "Leg"})

    def test_the_bodyless_allowlist_only_names_troops_that_exist(self):
        """An allowlist entry for a renamed or deleted troop rots silently and
        quietly widens the exemption."""
        troops = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "troops"
        if not troops.is_dir():
            self.skipTest("troop data not present")
        ids = set()
        for f in troops.glob("troops_*.xml"):
            ids |= set(re.findall(r'<NPCCharacter[^>]*?\sid="([^"]+)"',
                                  f.read_text(encoding="utf-8-sig", errors="ignore")))
        # An empty or broken scan reports EVERY allowlisted id as stale, which
        # reads exactly like a real finding. Fail on the scan first, so a bad
        # path can never masquerade as a data defect. (It already did once: a
        # backspace byte smuggled into this regex made it match nothing and the
        # failure named three troops that were present all along.)
        self.assertGreater(len(ids), 100,
                           f"scanned {troops} and found only {len(ids)} troop ids; "
                           "the scan is broken, not the allowlist")
        stale = sorted(set(ts.Validator._BODYLESS_BY_DESIGN) - ids)
        self.assertEqual(stale, [], f"allowlisted troops no longer exist: {stale}")

    def test_only_troop_files_are_scanned(self):
        """The check is about troop trees. Notables and lord rosters legitimately
        vary, and sweeping them would bury the signal."""
        _write(self.md / "characters" / "npcs_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="spc_notable_x" culture="Culture.umbar">\n'
               '    <Equipments><EquipmentRoster>'
               '<equipment slot="Head" id="Item.good_helm" />'
               "</EquipmentRoster></Equipments>\n"
               "  </NPCCharacter>\n</NPCCharacters>\n")
        self.assertEqual(self._codes("MISSING_BODY_ARMOUR"), [])


class TroopPurposeTests(unittest.TestCase):
    """TROOP_SKILL_KIT_MISMATCH and TROOP_ITEM_ZERO_NOT_MAIN (#343): the Item0 weapon is the one in hand
    at spawn, so it must be a real weapon and its class must hold the troop's top weapon skill."""

    CLASSES = {"sword": "OneHanded", "spear": "Polearm", "great": "TwoHanded", "bow": "Bow",
               "arrows": "Arrows", "jav": "Throwing", "shield": "Shield"}

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _issues(self, sets, skills, tid="umbar_x", group="Infantry", attrs="", classes=None,
                fname="troops/troops_test.xml", couch=None):
        rosters = "".join("<EquipmentRoster>" + "".join(
            f'<equipment slot="{k}" id="Item.{v}" />' for k, v in s.items()) + "</EquipmentRoster>"
            for s in sets)
        rows = "".join(f'<skill id="{k}" value="{v}" />' for k, v in skills.items())
        _write(self.md / fname,
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               f'  <NPCCharacter id="{tid}" default_group="{group}" level="21" culture="Culture.umbar"{attrs}>\n'
               f"    <skills>{rows}</skills>\n    <Equipments>{rosters}</Equipments>\n"
               "  </NPCCharacter>\n</NPCCharacters>\n")
        reg = ts.Registries(items=set(), item_def_files={}, npccharacters=set(), cultures={"umbar"},
                            party_templates=set(),
                            item_classes=self.CLASSES if classes is None else classes, couchable_ids=couch)
        return [i for i in ts.Validator(self.md, self.schemas, reg).run()
                if i.code in ("TROOP_SKILL_KIT_MISMATCH", "TROOP_ITEM_ZERO_NOT_MAIN", "TROOP_MAIN_WEAPON_NOT_ITEM0")]

    def test_a_swordsman_with_polearm_on_top_is_reported(self):
        found = self._issues([{"Item0": "sword", "Item1": "shield"}],
                             {"OneHanded": 230, "TwoHanded": 210, "Polearm": 235})
        self.assertEqual([i.code for i in found], ["TROOP_SKILL_KIT_MISMATCH"])
        self.assertIs(found[0].severity, ts.TROOP_SKILL_KIT_MISMATCH_SEVERITY)
        self.assertIn("OneHanded 230", found[0].message)
        self.assertIn("--fix-purpose", found[0].message)

    def test_a_purpose_correct_troop_is_clean(self):
        self.assertEqual(self._issues([{"Item0": "great", "Item1": "sword"}],
                                      {"OneHanded": 210, "TwoHanded": 235, "Polearm": 200}), [])

    def test_an_archer_under_its_melee_skill_is_explained_not_fixed(self):
        found = self._issues([{"Item0": "bow", "Item1": "arrows", "Item2": "sword"}],
                             {"OneHanded": 130, "Bow": 105}, group="Ranged")
        self.assertEqual([i.code for i in found], ["TROOP_SKILL_KIT_MISMATCH"])
        self.assertIn("melee skill above the launcher's ladder cell", found[0].message)

    def test_a_shield_in_item0_beside_a_sword_is_an_error(self):
        found = self._issues([{"Item0": "shield", "Item1": "sword"}], {"OneHanded": 100})
        self.assertEqual([(i.code, i.severity) for i in found],
                         [("TROOP_ITEM_ZERO_NOT_MAIN", ts.Severity.ERROR)])
        self.assertIn("Shield", found[0].message)
        self.assertIn("no main class", found[0].message)
        self.assertIn("--fix-purpose", found[0].message)

    def test_an_archer_opening_with_a_sword_is_a_warning_no_tool_repairs(self):
        sets = [{"Item0": "sword", "Item1": "bow", "Item2": "arrows"}]
        found = self._issues(sets, {"OneHanded": 150, "Bow": 100}, group="Ranged")
        self.assertEqual([(i.code, i.severity) for i in found],
                         [("TROOP_SKILL_KIT_MISMATCH", ts.TROOP_SKILL_KIT_MISMATCH_SEVERITY)])
        self.assertIn("launcher troop that does not open with its launcher", found[0].message)
        self.assertIn("No tool repairs", found[0].message)
        self.assertNotIn("--fix-purpose", found[0].message)

    def test_a_javelin_in_item0_is_a_throwers_main_weapon(self):
        clean = self._issues([{"Item0": "jav", "Item1": "jav", "Item2": "sword", "Item3": "shield"}],
                             {"OneHanded": 100, "Throwing": 160})
        self.assertEqual(clean, [])

    def test_a_thrower_under_its_melee_skill_names_the_thrown_primary_list(self):
        sets = [{"Item0": "jav", "Item1": "jav", "Item2": "sword", "Item3": "shield"}]
        found = self._issues(sets, {"OneHanded": 250, "Polearm": 230, "Throwing": 55},
                             tid="gondor_har_javelineer")
        self.assertEqual([i.code for i in found], ["TROOP_SKILL_KIT_MISMATCH"])
        self.assertIn("Throwing 55", found[0].message)
        self.assertIn("in THROWN_PRIMARY_TROOP_IDS", found[0].message)
        other = self._issues(sets, {"OneHanded": 230, "Throwing": 55}, tid="gondor_loss_skirmisher")
        self.assertIn("not in THROWN_PRIMARY_TROOP_IDS", other[0].message)

    def test_the_writer_and_the_validator_read_the_same_battle_sets(self):
        # One reading: a roster typed Civilian never counts, and a troop-level row is laid over
        # every battle roster, in tools/rebalance_troops.py troop_kit as in this pass.
        import xml.etree.ElementTree as ET
        import rebalance_troops as rb
        body = ('<NPCCharacter id="umbar_x" default_group="Infantry" level="21">'
                '<skills><skill id="OneHanded" value="10" /><skill id="Polearm" value="300" /></skills>'
                '<Equipments><EquipmentRoster equipmentType="Civilian"><equipment slot="Item0" id="Item.spear" />'
                '</EquipmentRoster><EquipmentRoster><equipment slot="Item0" id="Item.shield" />'
                '<equipment slot="Item1" id="Item.spear" /></EquipmentRoster>'
                '<equipment slot="Item0" id="Item.sword" /></Equipments></NPCCharacter>')
        kit, _ = rb.troop_kit(ET.fromstring(body), self.CLASSES)
        self.assertEqual((kit.sets, kit.item0), (1, {"OneHanded": 1}))
        v = ts.Validator(self.md, self.schemas, ts.Registries(
            items=set(), item_def_files={}, npccharacters=set(), cultures=set(), party_templates=set()))
        self.assertEqual([{s: self.CLASSES[i] for s, i in k.items()} for k in v._battle_kits(body)],
                         [{"Item0": "OneHanded", "Item1": "Polearm"}])

    RIDER = {"Item0": "sword", "Item1": "shield", "Item2": "spear", "Horse": "steed"}
    RIDER_SKILLS = {"OneHanded": 230, "Polearm": 200}

    def test_a_lance_rider_that_opens_with_its_sword_is_a_warning_naming_the_mode(self):
        found = self._issues([self.RIDER], self.RIDER_SKILLS, group="Cavalry", couch=frozenset({"spear"}))
        self.assertEqual([(i.code, i.severity) for i in found],
                         [("TROOP_MAIN_WEAPON_NOT_ITEM0", ts.Severity.WARNING)])
        self.assertIn("--main-weapon-slots", found[0].message)
        self.assertIn("rule A", found[0].message)

    def test_a_rider_whose_spear_cannot_be_couched_is_not_judged(self):
        # The state before the Armory registers the spear's pieces under the couch description.
        self.assertEqual(self._issues([self.RIDER], self.RIDER_SKILLS, group="Cavalry", couch=frozenset()), [])

    def test_a_rider_that_already_opens_with_the_spear_is_clean(self):
        row = {"Item0": "spear", "Item1": "shield", "Item2": "sword", "Horse": "steed"}
        self.assertEqual(self._issues([row], {"OneHanded": 200, "Polearm": 230}, group="Cavalry",
                                      couch=frozenset({"spear"})), [])

    def test_a_troop_named_for_its_polearm_is_judged_without_a_couch(self):
        row = {"Item0": "sword", "Item1": "spear", "Item2": "shield"}
        found = self._issues([row], self.RIDER_SKILLS, tid="umbar_spearman", couch=frozenset())
        self.assertEqual([i.code for i in found], ["TROOP_MAIN_WEAPON_NOT_ITEM0"])
        self.assertIn("rule B", found[0].message)

    def test_the_fords_roster_is_a_second_root_that_gets_the_slot_rule_only(self):
        skills = {"OneHanded": 100, "Polearm": 300}     # a kit mismatch if this were troops/troops_*.xml
        found = self._issues([self.RIDER], skills, group="Cavalry", couch=frozenset({"spear"}),
                             fname="fords_of_war/fords_troops.xml")
        self.assertEqual([i.code for i in found], ["TROOP_MAIN_WEAPON_NOT_ITEM0"])
        (self.md / "fords_of_war" / "fords_troops.xml").unlink()
        other = self._issues([self.RIDER], skills, group="Cavalry", couch=frozenset({"spear"}),
                             fname="named_companions/named_companions.xml")
        self.assertEqual(other, [], "a file that is not listed in troop_purpose.EXTRA_TROOP_FILES is not read")

    def test_the_validator_reads_the_display_name_as_the_writer_does(self):
        row = {"Item0": "sword", "Item1": "spear", "Item2": "shield"}
        found = self._issues([row], self.RIDER_SKILLS, tid="umbar_guard", attrs=' name="{=k}Hill Spearman"',
                             couch=frozenset())
        self.assertEqual([i.code for i in found], ["TROOP_MAIN_WEAPON_NOT_ITEM0"])

    def test_without_a_couch_set_or_an_install_the_rule_is_not_judged(self):
        self.assertEqual(self._issues([self.RIDER], self.RIDER_SKILLS, group="Cavalry"), [])

    def test_the_writer_and_the_validator_plan_the_same_exchange(self):
        import xml.etree.ElementTree as ET
        import rebalance_troops as rb
        body = ('<NPCCharacter id="umbar_x" default_group="Cavalry" level="21"><skills /><Equipments>'
                '<EquipmentRoster><equipment slot="Item0" id="Item.sword" /><equipment slot="Item1" id="Item.shield" />'
                '<equipment slot="Item2" id="Item.spear" /></EquipmentRoster>'
                '<equipment slot="Horse" id="Item.steed" /></Equipments></NPCCharacter>')
        _, swaps, detail = rb.troop_kit_plan(ET.fromstring(body), self.CLASSES, fix_item0=True,
                                             couch_ids=frozenset({"spear"}))
        self.assertEqual((detail["plan"].rule, swaps), ("A", [(0, "Item2")]))
        found = self._issues([self.RIDER], self.RIDER_SKILLS, group="Cavalry", couch=frozenset({"spear"}))
        self.assertEqual(len(found), 1)

    def test_a_skipped_creature_and_a_hero_are_exempt(self):
        bad = ([{"Item0": "sword"}], {"OneHanded": 10, "Polearm": 300})
        self.assertEqual(self._issues(*bad, tid="cave_troll"), [])
        self.assertEqual(self._issues(*bad, attrs=' is_hero="true"'), [])

    def test_a_villager_outside_troops_is_not_judged(self):
        self.assertEqual(self._issues([{"Item0": "sword"}], {"OneHanded": 10, "Polearm": 300},
                                      fname="characters/npcs_test.xml"), [])

    def test_without_the_item_classes_nothing_is_judged(self):
        self.assertEqual(self._issues([{"Item0": "jav", "Item1": "sword"}], {"Polearm": 300},
                                      classes={}), [])


class WeaponSlotCoverageTests(unittest.TestCase):
    """INCONSISTENT_WEAPON_SLOT. The weapon half of the cross-set rule in
    `.claude/rules/troops.md`: slots 0-3 draw independently from the battle sets, so a
    launcher or its ammo present in some sets only ships a half-kit (#529), and with no
    weapon slot filled in every set a draw can leave the troop unarmed."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        self.registries = ts.Registries(
            items=set(), item_def_files={}, npccharacters=set(), cultures={"umbar"},
            party_templates=set(),
            launchers={"good_bow": object()},
            item_trade={"good_arrows": ts.ItemTrade(True, (), "", 0, "Arrows")})

    def tearDown(self):
        self._tmp.cleanup()

    @staticmethod
    def _set(**slots):
        rows = "".join(f'<equipment slot="{k}" id="Item.{v}" />' for k, v in slots.items())
        return f"<EquipmentRoster>{rows}</EquipmentRoster>"

    def _issues(self, equipments, attrs="", registries=None):
        _write(self.md / "troops" / "troops_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               f'  <NPCCharacter id="umbar_x" default_group="Ranged" culture="Culture.umbar"{attrs}>\n'
               f"    <Equipments>{equipments}</Equipments>\n"
               "  </NPCCharacter>\n</NPCCharacters>\n")
        validator = ts.Validator(self.md, self.schemas, registries or self.registries)
        return [i for i in validator.run() if i.code == "INCONSISTENT_WEAPON_SLOT"]

    def test_launcher_in_one_set_and_a_sword_in_the_same_slot_of_another_warns(self):
        found = self._issues(self._set(Item0="good_bow", Item1="good_arrows")
                             + self._set(Item0="sword", Item1="good_arrows"))
        self.assertEqual(len(found), 1)
        self.assertIn("Item0", found[0].message)
        self.assertIs(found[0].severity, ts.Severity.WARNING)

    def test_ammo_at_two_indices_warns(self):
        found = self._issues(self._set(Item0="good_bow", Item1="good_arrows")
                             + self._set(Item0="good_bow", Item2="good_arrows"))
        self.assertEqual(len(found), 1)
        self.assertIn("ammo", found[0].message)

    def test_ammo_in_some_sets_only_warns(self):
        found = self._issues(self._set(Item0="good_bow", Item1="good_arrows")
                             + self._set(Item0="good_bow", Item1="sword"))
        self.assertEqual(len(found), 1)
        self.assertIn("ammo", found[0].message)

    def test_consistent_archer_sets_are_clean(self):
        kit = self._set(Item0="good_bow", Item1="good_arrows", Item2="sword")
        self.assertEqual(self._issues(kit + kit), [])

    def test_no_weapon_slot_filled_in_every_set_warns(self):
        found = self._issues(self._set(Item0="sword") + self._set(Item1="sword"))
        self.assertEqual(len(found), 1)
        self.assertIn("every battle set", found[0].message)

    def test_a_hero_is_exempt(self):
        self.assertEqual(
            self._issues(self._set(Item0="sword") + self._set(Item1="sword"), attrs=' is_hero="true"'), [])

    def test_a_single_battle_set_is_clean(self):
        self.assertEqual(self._issues(self._set(Item0="sword")), [])

    def test_a_civilian_set_does_not_count(self):
        civ = '<EquipmentRoster civilian="true"><equipment slot="Item3" id="Item.sword" /></EquipmentRoster>'
        self.assertEqual(self._issues(self._set(Item0="sword") + self._set(Item0="sword") + civ), [])

    def test_a_troop_level_equipment_fills_the_slot_in_every_set(self):
        """Troop-level <equipment> lies over every set (data-scripts.md), so Item0 is filled
        in both sets even though the second roster leaves it empty."""
        found = self._issues('<equipment slot="Item0" id="Item.sword" />'
                             + self._set(Item1="sword") + self._set(Item2="sword"))
        self.assertEqual(found, [])

    def test_a_troop_level_equipment_with_an_empty_id_clears_the_slot_in_every_set(self):
        """An empty id clears the slot (data-scripts.md): Item0 is gone from both sets, so no slot
        index is filled in both."""
        found = self._issues('<equipment slot="Item0" id="" />'
                             + self._set(Item0="sword") + self._set(Item0="sword", Item1="sword"))
        self.assertEqual(len(found), 1)
        self.assertIn("every battle set", found[0].message)

    def test_without_launcher_data_only_the_slot_rule_runs(self):
        bare = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures={"umbar"}, party_templates=set())
        sets = self._set(Item0="good_bow") + self._set(Item0="sword")
        self.assertEqual(self._issues(sets, registries=bare), [])
        self.assertEqual(len(self._issues(self._set(Item0="a") + self._set(Item1="a"), registries=bare)), 1)

    def test_a_troop_with_no_weapon_in_any_set_is_not_flagged(self):
        self.assertEqual(self._issues(self._set(Body="mail") + self._set(Body="mail")), [])


class TownsfolkBattleRosterTests(unittest.TestCase):
    """TOWNSFOLK_WITHOUT_BATTLE_ROSTER. A character whose inline Equipments hold civilian
    rosters only has no battle set, so GetRandomEquipmentElements returns an empty kit and it
    stands naked in the arena (#295)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        self.registries = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                                        cultures={"umbar"}, party_templates=set())

    def tearDown(self):
        self._tmp.cleanup()

    def _found(self, folder, inner):
        _write(self.md / folder / "npcs_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="townsman_umbar" culture="Culture.umbar">\n'
               f"    <Equipments>{inner}</Equipments>\n  </NPCCharacter>\n</NPCCharacters>\n")
        issues = ts.Validator(self.md, self.schemas, self.registries).run()
        return [i for i in issues if i.code == "TOWNSFOLK_WITHOUT_BATTLE_ROSTER"]

    CIV = '<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.rags" /></EquipmentRoster>'
    BATTLE = '<EquipmentRoster><equipment slot="Body" id="Item.rags" /></EquipmentRoster>'

    def test_civilian_only_is_a_warning(self):
        found = self._found("characters", self.CIV)
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "townsman_umbar")
        self.assertIs(found[0].severity, ts.Severity.WARNING)

    def test_a_plain_twin_is_clean(self):
        self.assertEqual(self._found("characters", self.CIV + self.BATTLE), [])

    def test_no_equipment_block_is_clean(self):
        self.assertEqual(self._found("characters", ""), [])

    def test_troop_files_are_not_scanned(self):
        self.assertEqual(self._found("troops", self.CIV), [])


class TroopIdRemovedTests(unittest.TestCase):
    """TROOP_ID_REMOVED. A shipped troop id that leaves the troops files leaves a hollow
    CharacterObject in every save that held it (#670)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.md = self.root / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)
        self.registries = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                                        cultures={"umbar"}, party_templates=set())
        _write(self.md / "troops" / "troops_test.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               '  <NPCCharacter id="umbar_kept" culture="Culture.umbar"/>\n'
               "  <!-- <NPCCharacter id=\"umbar_commented\" culture=\"Culture.umbar\"/> -->\n"
               "</NPCCharacters>\n")

    def tearDown(self):
        self._tmp.cleanup()

    def _found(self, baseline):
        path = self.root / "ids.txt"
        if baseline is not None:
            path.write_text(baseline, encoding="utf-8")
        validator = ts.Validator(self.md, self.schemas, self.registries, shipped_troop_ids_file=path)
        return [i for i in validator.run() if i.code == "TROOP_ID_REMOVED"]

    def test_a_missing_baseline_id_is_an_error(self):
        found = self._found("# tag: v1\numbar_kept\numbar_gone\n")
        self.assertEqual([i.entry_id for i in found], ["umbar_gone"])
        self.assertIs(found[0].severity, ts.Severity.ERROR)

    def test_a_commented_out_definition_does_not_count(self):
        self.assertEqual([i.entry_id for i in self._found("umbar_commented\n")], ["umbar_commented"])

    def test_an_exception_with_a_reason_passes(self):
        self.assertEqual(self._found("umbar_gone\n[exceptions]\numbar_gone | accepted by Mike\n"), [])

    def test_an_exception_without_a_reason_does_not_pass(self):
        self.assertEqual(len(self._found("umbar_gone\n[exceptions]\numbar_gone\n")), 1)

    def test_no_baseline_file_is_one_error_not_a_silent_pass(self):
        found = self._found(None)
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].entry_id, "(baseline)")

    def test_a_baseline_holding_no_id_is_one_error_not_a_silent_pass(self):
        for text in ("# tag: v1\n[exceptions]\n", "", "[exceptions]\numbar_gone | accepted\numbar_kept\n"):
            found = self._found(text)
            self.assertEqual([i.entry_id for i in found], ["(baseline)"], repr(text))
            self.assertIs(found[0].severity, ts.Severity.ERROR)
            self.assertIn("ids.txt", found[0].message)

    def test_the_baseline_survives_a_write_and_read_with_its_exceptions(self):
        path = self.root / "roundtrip.txt"
        path.write_bytes(b"\xef\xbb\xbf# tag: v1\r\numbar_old\r\n[exceptions]\r\numbar_old | retired on purpose\r\n")
        ids, exceptions = ts.read_shipped_troop_ids(path)
        self.assertEqual((ids, exceptions), (["umbar_old"], {"umbar_old": "retired on purpose"}))
        ts.write_shipped_troop_ids(path, "v2", ["umbar_b", "umbar_a", "umbar_b"], exceptions)
        raw = path.read_bytes()
        self.assertFalse(raw.startswith(b"\xef\xbb\xbf"))
        self.assertNotIn(b"\r", raw)
        ids, exceptions = ts.read_shipped_troop_ids(path)
        self.assertEqual(ids, ["umbar_a", "umbar_b"])
        self.assertEqual(exceptions, {"umbar_old": "retired on purpose"})
        self.assertIn("# tag: v2", raw.decode("utf-8"))

    def test_a_single_quoted_or_spaced_id_counts_as_present(self):
        for spelling in ("<NPCCharacter id='kept'/>", '<NPCCharacter id = "kept"/>'):
            self.assertEqual(ts.Validator._TROOP_ID_RE.findall(spelling), ["kept"], spelling)

    def test_regenerating_keeps_the_exceptions_and_reads_the_tag_not_the_tree(self):
        target = self.root / "regen.txt"
        ts.write_shipped_troop_ids(target, "v1", ["umbar_old"], {"umbar_old": "retired on purpose"})
        xml = ('<NPCCharacters><NPCCharacter id="umbar_new" culture="Culture.umbar"/>'
               '<!-- <NPCCharacter id="umbar_commented"/> --></NPCCharacters>')

        def fake_git(cmd, **kwargs):
            args = cmd[3:]
            if args[0] == "show":
                self.assertEqual(args[1], "v9:Main/_Module/ModuleData/troops/troops_x.xml")
            out = {"describe": "v9\n", "ls-tree": "Main/_Module/ModuleData/troops/troops_x.xml\n",
                   "show": xml}[args[0]]
            return subprocess.CompletedProcess(cmd, 0, stdout=out.encode("utf-8"), stderr=b"")

        with mock.patch.object(vm, "SHIPPED_TROOP_IDS", target), \
                mock.patch.object(subprocess, "run", side_effect=fake_git), redirect_stdout(io.StringIO()):
            self.assertEqual(vm.regenerate_shipped_troop_ids(), 0)
        ids, exceptions = ts.read_shipped_troop_ids(target)
        self.assertEqual(ids, ["umbar_new"])
        self.assertEqual(exceptions, {"umbar_old": "retired on purpose"})
        self.assertIn("# tag: v9", target.read_text(encoding="utf-8"))

    def test_the_pass_is_off_without_a_baseline_argument(self):
        issues = ts.Validator(self.md, self.schemas, self.registries).run()
        self.assertEqual([i for i in issues if i.code == "TROOP_ID_REMOVED"], [])

    def test_the_shipped_baseline_parses_and_names_only_defined_or_excepted_ids(self):
        """The real file against the real tree: this is the gate, run as a test."""
        repo = Path(__file__).resolve().parents[2]
        baseline = repo / "tools" / "shipped_troop_ids.txt"
        troops = repo / "Main" / "_Module" / "ModuleData"
        if not baseline.is_file() or not troops.is_dir():
            self.skipTest("baseline or troop data not present")
        ids, exceptions = ts.read_shipped_troop_ids(baseline)
        self.assertGreater(len(ids), 500)
        validator = ts.Validator(troops, self.schemas, self.registries, shipped_troop_ids_file=baseline)
        missing = [i.entry_id for i in validator._troop_ids_removed()]
        self.assertEqual(missing, [], f"shipped troop ids gone from the troops files: {missing}")
        self.assertTrue(all(reason.strip() for reason in exceptions.values()))


class AppliesToCoverageTests(unittest.TestCase):
    """Every file that defines an <NPCCharacter> must match an `applies_to` glob of
    taom_npccharacter.json, or its duplicate-id and enum checks silently skip it."""

    def test_every_npccharacter_file_is_covered(self):
        md = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData"
        if not md.is_dir():
            self.skipTest("ModuleData not present")
        schema = next(s for s in ts.load_schemas(SCHEMA_DIR) if s.name == "taom_npccharacter")
        import fnmatch
        comment = re.compile(r"<!--.*?-->", re.S)
        defines = re.compile(r"<NPCCharacter\b")
        files = []
        for path in sorted(md.rglob("*.xml")):
            text = comment.sub("", path.read_text(encoding="utf-8-sig", errors="ignore"))
            if defines.search(text):
                files.append(path.relative_to(md).as_posix())
        self.assertGreater(len(files), 30, "the scan found almost no NPCCharacter files")
        uncovered = [f for f in files if not any(fnmatch.fnmatch(f, g) for g in schema.applies_to)]
        self.assertEqual(uncovered, [], "these files define <NPCCharacter> outside every applies_to "
                         f"glob of tools/schemas/taom_npccharacter.json: {uncovered}")


class MountWithoutHarnessTests(unittest.TestCase):
    """MOUNT_WITHOUT_HARNESS. A Horse slot requires a HorseHarness slot in the same
    equipment set. The harness is not only armour: on some mounts it carries the
    rider's SEAT, and nothing in the XML says which, because where the saddle is
    modelled is a property of a mesh this validator cannot see. The war ram proved
    it. `sk_eb_goat_a` and `_b` are bare pelts and every saddle lives on one of the
    eight `sk_eb_goat_bard_*` harness meshes, so `ironpass_ram_herder` shipped four
    sets riding bareback and players reported it.

    Exemptions are named in `_HARNESSLESS_BY_DESIGN`, each with a reason."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        self.md.mkdir(parents=True)
        self.registries = ts.Registries(
            items={"None", "good_axe", "charger", "chain_horse_harness",
                   "taom_war_ram_a", "taom_war_ram_b", "taom_ram_barding_light_a"},
            item_def_files={},
            npccharacters=set(),
            cultures={"erebor", "vlandia"},
            party_templates=set(),
        )
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _found(self, registries=None):
        reg = registries if registries is not None else self.registries
        issues = ts.Validator(self.md, self.schemas, reg).run()
        return [i for i in issues if i.code == "MOUNT_WITHOUT_HARNESS"]

    def _write_troop(self, *sets, troop_id="ironpass_ram_herder"):
        """One NPCCharacter carrying `sets` EquipmentRosters. Each set is a list of
        (slot, item) pairs, matching the lowercase <equipment> the troops files use."""
        rosters = ""
        for pairs in sets:
            rosters += "      <EquipmentRoster>" + "\n"
            for slot, item in pairs:
                rosters += f'        <equipment slot="{slot}" id="Item.{item}" />' + "\n"
            rosters += "      </EquipmentRoster>" + "\n"
        _write(self.md / "troops" / "troops_erebor.xml",
               '<?xml version="1.0"?>' + "\n" + "<NPCCharacters>" + "\n"
               + f'  <NPCCharacter id="{troop_id}" race="dwarf" occupation="Soldier" '
               + 'culture="Culture.erebor" default_group="Cavalry">' + "\n"
               + "    <Equipments>" + "\n" + rosters + "    </Equipments>" + "\n"
               + "  </NPCCharacter>" + "\n" + "</NPCCharacters>" + "\n")

    def test_a_bare_ram_is_reported(self):
        self._write_troop([("Item0", "good_axe"), ("Horse", "taom_war_ram_a")])
        found = self._found()
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0].severity, ts.Severity.ERROR)
        self.assertIn("taom_war_ram_a", found[0].message)

    def test_a_barded_ram_is_clean(self):
        self._write_troop([("Horse", "taom_war_ram_a"),
                           ("HorseHarness", "taom_ram_barding_light_a")])
        self.assertEqual(self._found(), [])

    def test_an_ordinary_horse_without_a_harness_is_also_reported(self):
        """The rule is universal, not ram-only. A vanilla horse renders its own
        saddle, so this rider is not bareback, but an unbarded mount is still a
        decision to make on purpose rather than by omission. Eomer reached players
        wearing the full Theoden kit on a bare charger while all fourteen of his
        Rohan siblings carried a harness."""
        self._write_troop([("Horse", "charger")], troop_id="rohan_lord")
        found = self._found()
        self.assertEqual(len(found), 1)
        self.assertIn("charger", found[0].message)

    def test_a_harnessed_horse_is_clean(self):
        self._write_troop([("Horse", "charger"), ("HorseHarness", "chain_horse_harness")],
                          troop_id="rohan_lord")
        self.assertEqual(self._found(), [])

    def test_a_footed_troop_is_clean(self):
        self._write_troop([("Item0", "good_axe")], troop_id="ironpass_warrior")
        self.assertEqual(self._found(), [])

    def test_the_finding_names_the_troop_not_just_the_mount(self):
        self._write_troop([("Horse", "taom_war_ram_b")])
        self.assertEqual(self._found()[0].entry_id, "ironpass_ram_herder")

    def test_each_set_is_judged_on_its_own(self):
        # The engine draws every slot from an independently chosen set, so barding
        # three sets of four still spawns bare rams. Exactly one finding, not zero.
        self._write_troop(
            [("Horse", "taom_war_ram_a"), ("HorseHarness", "taom_ram_barding_light_a")],
            [("Horse", "taom_war_ram_b"), ("HorseHarness", "taom_ram_barding_light_a")],
            [("Horse", "taom_war_ram_b"), ("HorseHarness", "taom_ram_barding_light_a")],
            [("Horse", "taom_war_ram_a")],
        )
        self.assertEqual(len(self._found()), 1)

    def test_a_by_design_owner_is_exempt(self):
        self._write_troop([("Horse", "taom_war_ram_a")], troop_id="harad_mumakil_rider")
        self.assertEqual(self._found(), [])

    def test_every_by_design_entry_states_a_reason(self):
        """An exemption with no reason is just a silenced finding."""
        self.assertTrue(ts.Validator._HARNESSLESS_BY_DESIGN)
        for owner, reason in ts.Validator._HARNESSLESS_BY_DESIGN.items():
            self.assertGreater(len(reason), 40, f"{owner} records no real reason")

    def test_the_by_design_allowlist_only_names_owners_that_exist(self):
        """An allowlist entry for a renamed or deleted owner rots silently and
        quietly widens the exemption."""
        md = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData"
        if not md.is_dir():
            self.skipTest("ModuleData not present")
        ids = set()
        for f in md.rglob("*.xml"):
            ids |= set(re.findall(r'<NPCCharacter[^>]*?\sid="([^"]+)"',
                                  f.read_text(encoding="utf-8-sig", errors="ignore")))
        # A broken scan reports every allowlisted id as stale, which reads exactly
        # like a real finding. Fail on the scan first, so a bad path can never
        # masquerade as a data defect.
        self.assertGreater(len(ids), 100,
                           f"scanned {md} and found only {len(ids)} ids; the scan is broken")
        stale = sorted(set(ts.Validator._HARNESSLESS_BY_DESIGN) - ids)
        self.assertEqual(stale, [], f"allowlisted owners no longer exist: {stale}")

    def test_it_still_reports_without_an_installed_game(self):
        """The reason this check must not sit behind the mount_family_types guard.

        That registry comes from the INSTALLED modules, and LOTRLOME_Armory (which
        defines the ram and its bardings) is untracked and reverts on a reinstall.
        Gating on it would silence the check in exactly the situation where the data
        is most likely broken, while the validator still printed PASS."""
        self._write_troop([("Horse", "taom_war_ram_a")])
        degraded = ts.Registries(
            items=set(), item_def_files={}, npccharacters=set(),
            cultures={"erebor"}, party_templates=set(),
        )
        self.assertEqual(degraded.mount_family_types, {})
        self.assertEqual(len(self._found(registries=degraded)), 1)


class MissingCollisionBodyTests(unittest.TestCase):
    """The #352 / #599 hang class as a validator ERROR: an item or crafting
    piece whose `body_name` (or holster / collision body) names a PhysicsShape
    no loaded tpac ships. `validate_mesh_refs.py` Tier C is the engine; this
    pass only turns its MISSING_BODY findings into ERROR issues (and its
    MISSING_MESH into WARNINGs) so the commit hook sees them without anyone
    remembering a second command (the MCP tool too since #623)."""

    def test_loaded_tpacs_keeps_the_modules_path_it_was_given(self):
        # rebuilding "<parent>/Modules" from another spelling of the folder ("mods", a lowercase "modules") would
        # list packages under a path the caller never gave
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            mods = Path(d) / "mods"
            (mods / "LOTRLOME_Armory" / "Assets").mkdir(parents=True)
            (mods / "LOTRLOME_Armory" / "Assets" / "a.tpac").write_bytes(b"x")
            got = vm._loaded_tpacs(mods)
            self.assertEqual(got, [mods / "LOTRLOME_Armory" / "Assets" / "a.tpac"])

    def test_loaded_tpacs_reads_taom_map_packs(self):
        with tempfile.TemporaryDirectory() as d:
            mods = Path(d) / "Modules"
            for name in ("LOTRLOME_Armory", "TAOM_Map", "Native"):
                (mods / name / "Assets").mkdir(parents=True)
                (mods / name / "Assets" / f"{name}.tpac").write_bytes(b"x")
            got = {p.name for p in vm._loaded_tpacs(mods)}
        self.assertEqual(got, {"LOTRLOME_Armory.tpac", "TAOM_Map.tpac", "Native.tpac"})

    def test_loaded_tpacs_takes_taom_from_the_repo_module(self):
        # The commit gate judges the repo's tree, as slot_rejection_issues does, not the last deploy.
        with tempfile.TemporaryDirectory() as d:
            mods = Path(d) / "Modules"
            (mods / "TAOM" / "Assets").mkdir(parents=True)
            (mods / "TAOM" / "Assets" / "installed.tpac").write_bytes(b"x")
            repo = Path(d) / "repo" / "_Module"
            (repo / "Assets").mkdir(parents=True)
            (repo / "Assets" / "repo.tpac").write_bytes(b"x")
            (repo / "ModuleData").mkdir()
            got = {p.name for p in vm._loaded_tpacs(mods, repo / "ModuleData")}
        self.assertEqual(got, {"repo.tpac"})

    def _fake_tier_c(self, codes):
        import validate_mesh_refs as vmr
        issues = []
        for code, item, name in codes:
            sev = vmr.Severity.ERROR if code in ("MISSING_BODY", "MISSING_MESH") else vmr.Severity.WARNING
            issues.append(vmr.Issue(severity=sev, code=code, file="LOTRLOME_items/x.xml", line=7,
                                    entry_id=item, message=f"{code}: {name} not in any tpac"))
        return issues

    def test_missing_body_becomes_error_and_missing_mesh_a_warning(self):
        from unittest import mock
        import validate_mesh_refs as vmr
        fake = self._fake_tier_c([("MISSING_BODY", "starter_highelf_longbow", "bo_wm_elven_bow_v1"),
                                  ("MISSING_MESH", "starter_highelf_longbow", "wm_elven_bow_v1"),
                                  ("KNOWN_DEAD_MESH", "old_item", "old_mesh")])
        with mock.patch.object(vmr, "extract_refs", return_value=[]),              mock.patch.object(vmr, "build_present_set", return_value=vmr.PresentSet(tpac_paths=["a.tpac"])),              mock.patch.object(vmr, "classify", return_value=fake),              mock.patch.object(vm, "_loaded_tpacs", return_value=[Path("a.tpac")]):
            issues = vm.missing_collision_body_issues(Path("game/Modules"), Path("md"))
        by_code = {i.code: i for i in issues}
        self.assertEqual(by_code[vm.BODY_CODE].severity, ts.Severity.ERROR)
        self.assertIn("bo_wm_elven_bow_v1", by_code[vm.BODY_CODE].message)
        self.assertIn("#352", by_code[vm.BODY_CODE].message)
        self.assertEqual(by_code[vm.BODY_CODE].entry_id, "starter_highelf_longbow")
        self.assertEqual(by_code[vm.MESH_CODE].severity, ts.Severity.WARNING)
        self.assertNotIn("KNOWN_DEAD_MESH", by_code)          # allowlist noise stays in the sibling tool

    def test_no_tpacs_is_a_finding_not_a_pass(self):
        """A present-set built from zero packs would report every body missing
        (or, filtered, nothing at all); either way it is not evidence."""
        from unittest import mock
        with mock.patch.object(vm, "_loaded_tpacs", return_value=[]):
            issues = vm.missing_collision_body_issues(Path("game/Modules"), Path("md"))
        self.assertEqual([i.code for i in issues], [vm.BODY_CODE])
        self.assertIn("NOT verified", issues[0].message)

    def test_fallback_scans_only_the_packs_that_failed_to_parse(self):
        """A parsed pack's TOC already lists its bodies (#352), so only a pack that failed to
        parse can hide one. Byte-scanning every pack instead took 110-119 s over 4,611 packs
        (22.6 GiB), past the commit hook's 45 s bound, in exactly the #599 case."""
        import validate_mesh_refs as vmr
        present = vmr.PresentSet(physicsshapes={"bo_a"}, tpac_paths=["good.tpac", "bad.tpac"],
                                 unparsed=[("bad.tpac", "suspicious udep_count")])
        ref = vmr.MeshRef(name="bo_missing", attr="body_name", kind="collision_body",
                          file="LOTRLOME_items/x.xml", line=7, item_id="it", culture="")
        with mock.patch.object(vmr, "extract_refs", return_value=[ref]), \
             mock.patch.object(vmr, "build_present_set", return_value=present), \
             mock.patch.object(vmr, "bodies_present_in_tpacs", return_value=set()) as scan, \
             mock.patch.object(vm, "_loaded_tpacs", return_value=[Path("good.tpac"), Path("bad.tpac")]):
            issues = vm.missing_collision_body_issues(Path("game/Modules"), Path("md"))
        scan.assert_called_once()
        self.assertEqual(list(scan.call_args.args[1]), [Path("bad.tpac")])
        self.assertEqual([(i.code, i.entry_id) for i in issues], [(vm.BODY_CODE, "it")])

    def test_a_crash_inside_the_pass_is_a_not_verified_error(self):
        """A scan that raises must say the bodies went unverified, as the generator pass does,
        not escape as a traceback the hook turns into a blocked commit with no findings."""
        import validate_mesh_refs as vmr
        with mock.patch.object(vm, "_loaded_tpacs", return_value=[Path("a.tpac")]), \
             mock.patch.object(vmr, "extract_refs", side_effect=MemoryError("corrupt sized string")):
            issues = vm.missing_collision_body_issues(Path("game/Modules"), Path("md"))
        self.assertEqual([(i.code, i.severity) for i in issues], [(vm.BODY_CODE, ts.Severity.ERROR)])
        self.assertIn("NOT verified", issues[0].message)
        self.assertIn("MemoryError", issues[0].message)

    def test_main_runs_the_pass(self):
        """#622: main() never called this pass, so MISSING_COLLISION_BODY never fired and the
        commit hook's --code line for it blocked nothing, while this class stayed green.
        Drive main(), not the function; exit 1 is the code the hook blocks on."""
        import validate_mesh_refs as vmr
        fake = self._fake_tier_c([("MISSING_BODY", "starter_highelf_longbow", "bo_wm_elven_bow_v1")])
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "ModuleData").mkdir()
            (root / "Game" / "Modules").mkdir(parents=True)
            with mock.patch.object(vmr, "extract_refs", return_value=[]), \
                 mock.patch.object(vmr, "build_present_set", return_value=vmr.PresentSet(tpac_paths=["a.tpac"])), \
                 mock.patch.object(vmr, "classify", return_value=fake), \
                 mock.patch.object(vm, "_loaded_tpacs", return_value=[Path("a.tpac")]), \
                 mock.patch.object(vm, "generator_item_ref_issues", return_value=[]):
                code, out, err = _run_main("--moduledata", str(root / "ModuleData"),
                                           "--game-modules", str(root / "Game" / "Modules"),
                                           "--code", vm.BODY_CODE)
        self.assertIn(vm.BODY_CODE, out, out + err)
        self.assertIn("bo_wm_elven_bow_v1", out)
        self.assertEqual(code, 1)

    def test_main_without_install_says_the_pass_was_skipped(self):
        """Without the install there are no tpacs to scan: the run must say this gate did not
        run, never print nothing about it; exit 2 is the degraded-sweep code."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "ModuleData").mkdir()
            code, _, err = _run_main("--moduledata", str(root / "ModuleData"),
                                     "--game-modules", str(root / "nowhere"), "--code", vm.BODY_CODE)
        self.assertIn(f"{vm.BODY_CODE} SKIPPED", err)
        self.assertEqual(code, 2)

    def test_main_without_install_names_the_skipped_ref_checks(self):
        """The no-install warning lists what it skips; the faction and body-property checks
        were skipped without being named, which reads like a clean sweep."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "ModuleData").mkdir()
            _, _, err = _run_main("--moduledata", str(root / "ModuleData"),
                                  "--game-modules", str(root / "nowhere"))
        self.assertIn("faction", err)
        self.assertIn("body-property", err)

    def test_registry_line_counts_the_clans(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "ModuleData").mkdir()
            _write(root / "ModuleData" / "characters" / "clans.xml",
                   '<?xml version="1.0"?>\n<Factions>\n  <Faction id="c1" />\n</Factions>\n')
            (root / "Modules").mkdir()
            _, _, err = _run_main("--moduledata", str(root / "ModuleData"),
                                  "--game-modules", str(root / "Modules"), "--code", "NO_SUCH_CODE")
        self.assertRegex(err, r"Registry: .*\d+ clans")


class PrisonSceneTests(unittest.TestCase):
    """PRISON_SCENE_NOT_STEALTH (2026-11-13). A 1.5.x prison break spawns its guards on the dungeon
    scene's stealth_agent points and its reinforcements on prison_break_reinforcement_point. Orthanc
    and Orthanc Gate named empire_dungeon_a, which has neither, while 278 prisons used a
    *_dungeon_stealth scene. TAOM_Map is unversioned, so this is the in-repo gate."""

    AGENT = '<tag name="stealth_agent"/>'
    REINFORCEMENT = '<variable name="SpawnGroupTag" value="prison_break_reinforcement_point"/>'
    STEALTH = f'<scene><entity>{AGENT}</entity><entity>{REINFORCEMENT}</entity></scene>'
    ONLY_AGENT = f'<scene><entity>{AGENT}</entity></scene>'
    ONLY_REINFORCEMENT = f'<scene><entity>{REINFORCEMENT}</entity></scene>'
    OLD = '<scene><entity name="chair"><tag name="npc_common"/></entity></scene>'

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.modules = Path(self._tmp.name) / "Modules"
        _write(self.modules / "SandBox" / "SceneObj" / "empire_dungeon_stealth" / "scene.xscene", self.STEALTH)
        _write(self.modules / "SandBox" / "SceneObj" / "empire_dungeon_a" / "scene.xscene", self.OLD)

    def tearDown(self):
        self._tmp.cleanup()

    def _settlements(self, *rows):
        body = "".join(
            f'<Settlement id="{sid}"><Locations complex_template="x">'
            f'<Location id="center" scene_name="empire_dungeon_a" />{loc}</Locations></Settlement>'
            for sid, loc in rows)
        path = self.modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
        path.parent.mkdir(parents=True, exist_ok=True)
        # The live file carries a BOM; the pass must read it.
        path.write_bytes(b"\xef\xbb\xbf" + f"<Settlements>{body}</Settlements>".encode("utf-8"))

    def _issues(self):
        return vm.prison_scene_issues(self.modules)

    def test_pre_stealth_prison_scene_is_an_error(self):
        self._settlements(("town_isengard", '<Location id="prison" scene_name="empire_dungeon_a" />'),
                          ("town_EW1", '<Location id="prison" scene_name="empire_dungeon_stealth" />'))
        found = self._issues()
        self.assertEqual([(i.entry_id, i.severity) for i in found], [("town_isengard", ts.Severity.ERROR)])
        self.assertEqual(found[0].code, vm.PRISON_CODE)
        self.assertIn("prison_break_reinforcement_point", found[0].message)
        # empire_dungeon_a is a key of the remap tool's table, so the repair names the tool.
        self.assertIn("remap_stale_scene_names.py --dry-run, then with --apply (a backup is made)",
                      found[0].message)

    def test_scene_with_only_the_agent_tag_names_the_missing_reinforcement_tag(self):
        _write(self.modules / "SandBox" / "SceneObj" / "half_a" / "scene.xscene", self.ONLY_AGENT)
        self._settlements(("town_a", '<Location id="prison" scene_name="half_a" />'))
        found = self._issues()
        self.assertEqual([i.severity for i in found], [ts.Severity.ERROR])
        self.assertIn("has no prison_break_reinforcement_point entities", found[0].message)
        self.assertNotIn("has no stealth_agent", found[0].message)

    def test_scene_with_only_the_reinforcement_tag_names_the_missing_agent_tag(self):
        _write(self.modules / "SandBox" / "SceneObj" / "half_b" / "scene.xscene", self.ONLY_REINFORCEMENT)
        self._settlements(("town_b", '<Location id="prison" scene_name="half_b" />'))
        found = self._issues()
        self.assertEqual([i.severity for i in found], [ts.Severity.ERROR])
        self.assertIn("has no stealth_agent entities", found[0].message)

    def test_repair_text_skips_the_remap_tool_for_a_scene_it_cannot_fix(self):
        # half_a is not a key of the remap tool's REMAP table.
        _write(self.modules / "SandBox" / "SceneObj" / "half_a" / "scene.xscene", self.ONLY_AGENT)
        self._settlements(("town_a", '<Location id="prison" scene_name="half_a" />'))
        message = self._issues()[0].message
        self.assertNotIn("remap_stale_scene_names", message)
        self.assertIn("point the prison at a *_dungeon_stealth scene", message)

    def test_unparsable_live_file_is_one_error_not_a_crash(self):
        path = self.modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
        path.parent.mkdir(parents=True)
        path.write_bytes(b"<Settlements><Settlement>")
        found = self._issues()
        self.assertEqual([i.severity for i in found], [ts.Severity.ERROR])
        self.assertIn("does not parse", found[0].message)
        self.assertIn("prison scenes NOT checked", found[0].message)

    def test_live_file_with_no_prison_rows_is_an_error(self):
        self._settlements(("town_EW1", ""))
        found = self._issues()
        self.assertEqual([i.severity for i in found], [ts.Severity.ERROR])
        self.assertIn("no prison Location rows in", found[0].message)
        self.assertIn("nothing checked", found[0].message)

    def test_level_slots_are_checked_and_scene_names_ignore_case(self):
        self._settlements(("castle_orthanc_gate",
                           '<Location id="prison" scene_name_1="Empire_Dungeon_Stealth" '
                           'scene_name_2="empire_dungeon_a" />'))
        self.assertEqual([i.entry_id for i in self._issues()], ["castle_orthanc_gate"])

    def test_other_locations_are_not_judged(self):
        # The fixture's center rows name empire_dungeon_a; only the prison row is in scope.
        self._settlements(("town_EW1", '<Location id="prison" scene_name="empire_dungeon_stealth" />'))
        self.assertEqual(self._issues(), [])

    def test_unknown_scene_warns_instead_of_passing(self):
        self._settlements(("town_EW1", '<Location id="prison" scene_name="no_such_dungeon" />'))
        found = self._issues()
        self.assertEqual([(i.entry_id, i.severity) for i in found], [("town_EW1", ts.Severity.WARNING)])

    def test_missing_live_file_warns_instead_of_passing(self):
        found = self._issues()
        self.assertEqual([i.severity for i in found], [ts.Severity.WARNING])
        self.assertIn("NOT checked", found[0].message)


class EntryPointCrashTests(unittest.TestCase):
    """An unexpected exception out of main() must exit 3 with the traceback on stderr. Python's own
    exit for an uncaught exception is 1, the code that means "ERROR findings", so the commit hook
    would block a commit over a validator bug and show the author a bare exit status."""

    def test_unexpected_exception_exits_3_with_the_traceback(self):
        err = io.StringIO()
        with mock.patch.object(vm, "main", side_effect=RuntimeError("registry exploded")), \
             redirect_stderr(err):
            code = vm.entry()
        self.assertEqual(code, 3)
        self.assertIn("Traceback", err.getvalue())
        self.assertIn("registry exploded", err.getvalue())

    def test_a_normal_exit_code_passes_through(self):
        for rc in (0, 1, 2):
            with self.subTest(rc=rc), mock.patch.object(vm, "main", return_value=rc):
                self.assertEqual(vm.entry(), rc)

    def test_argparse_exit_is_not_turned_into_a_crash(self):
        with mock.patch.object(vm, "main", side_effect=SystemExit(2)):
            with self.assertRaises(SystemExit) as cm:
                vm.entry()
        self.assertEqual(cm.exception.code, 2)

    def test_a_crash_while_loading_the_module_also_exits_3(self):
        # A broken taom_schema.py fails at import time, before entry() runs (the convergence
        # review of #949-#952 reproduced exit 1 this way).
        script = Path(vm.__file__).resolve()
        code = ("import sys, runpy; sys.modules['taom_schema'] = None; "
                f"sys.argv = [{str(script)!r}]; runpy.run_path({str(script)!r}, run_name='__main__')")
        run = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, timeout=120)
        self.assertEqual(run.returncode, 3, run.stderr[-2000:])
        self.assertIn("Traceback", run.stderr)


_FACTIONS_XSD = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Factions">
    <xs:complexType><xs:sequence>
      <xs:element name="Faction" minOccurs="0" maxOccurs="unbounded">
        <xs:complexType>
          <xs:attribute name="id" type="xs:string" use="required"/>
          <xs:attribute name="initial_home_settlement" type="xs:string" use="required"/>
        </xs:complexType>
      </xs:element>
    </xs:sequence></xs:complexType>
  </xs:element>
</xs:schema>
"""
_GOOD_CLANS = """<?xml version="1.0" encoding="utf-8"?>
<Factions>
  <Faction id="clan_a" initial_home_settlement="Settlement.town_A1"/>
</Factions>
"""
_REGISTRATION = """<?xml version="1.0" encoding="utf-8"?>
<Module><Xmls><XmlNode><XmlName id="Factions" path="characters/clans"/></XmlNode></Xmls></Module>
"""


class ArmourClassTableDriftTests(unittest.TestCase):
    """ARMOUR_CLASS_TABLE_DRIFT: the armour acquisition class table the game reads is generated
    from the unversioned Armory, so an art drop can leave it stale with nothing else noticing
    (docs/features/armour-acquisition.md). The pass warns on drift, reports a generator that
    cannot run, finds no armour, or reads a DIFFERENT Armory than the one under --game-modules,
    and skips (never silently passes) without the Armory at all."""

    def _armory(self, tmp):
        (Path(tmp) / "LOTRLOME_Armory").mkdir()
        return Path(tmp)

    def _expected_root(self, tmp):
        """The root gac.generate() must read for `_armory(tmp)` to count as verified."""
        return str(Path(tmp) / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items")

    def test_skipped_without_the_armory(self):
        with tempfile.TemporaryDirectory() as tmp:
            err = io.StringIO()
            with redirect_stderr(err):
                issues = vm.armour_class_table_issues(Path(tmp))
        self.assertEqual(issues, [])
        self.assertIn("ARMOUR_CLASS_TABLE_DRIFT SKIPPED", err.getvalue())
        self.assertIn("no LOTRLOME_Armory", err.getvalue())

    def test_skip_goes_to_the_callers_list_instead_of_stderr(self):
        with tempfile.TemporaryDirectory() as tmp:
            err = io.StringIO()
            skips = []
            with redirect_stderr(err):
                issues = vm.armour_class_table_issues(Path(tmp), None, skips)
        self.assertEqual(issues, [])
        self.assertEqual(err.getvalue(), "")
        self.assertEqual(len(skips), 1)
        self.assertIn("ARMOUR_CLASS_TABLE_DRIFT SKIPPED: no LOTRLOME_Armory", skips[0])

    def test_current_table_raises_nothing(self):
        import generate_armour_classes as gac
        import rebalance_armor as ra
        rows = {"a_piece": ("light", None)}
        with tempfile.TemporaryDirectory() as tmp:
            with mock.patch.object(ra, "ARMORY_DIR", self._expected_root(tmp)), \
                 mock.patch.object(gac, "generate", return_value=rows), \
                 mock.patch.object(gac, "read_committed", return_value=gac.render(rows)):
                self.assertEqual(vm.armour_class_table_issues(self._armory(tmp)), [])

    def test_stale_table_warns_and_names_the_drift(self):
        import generate_armour_classes as gac
        import rebalance_armor as ra
        committed = gac.render({"a_piece": ("light", None)})
        with tempfile.TemporaryDirectory() as tmp:
            with mock.patch.object(ra, "ARMORY_DIR", self._expected_root(tmp)), \
                 mock.patch.object(gac, "generate", return_value={"a_piece": ("heavy", None)}), \
                 mock.patch.object(gac, "read_committed", return_value=committed):
                issues = vm.armour_class_table_issues(self._armory(tmp))
        self.assertEqual([i.code for i in issues], [vm.ARMOUR_CLASS_CODE])
        self.assertIs(issues[0].severity, ts.Severity.WARNING)
        self.assertIn("1 changed (a_piece)", issues[0].message)

    def test_a_generator_that_cannot_run_is_reported_not_passed(self):
        import generate_armour_classes as gac
        with tempfile.TemporaryDirectory() as tmp:
            with mock.patch.object(gac, "generate", side_effect=RuntimeError("boom")):
                issues = vm.armour_class_table_issues(self._armory(tmp))
        self.assertEqual([i.code for i in issues], [vm.ARMOUR_CLASS_CODE])
        self.assertIs(issues[0].severity, ts.Severity.WARNING)
        self.assertIn("NOT verified", issues[0].message)
        self.assertIn("boom", issues[0].message)

    def test_generator_finding_no_armour_is_reported_not_passed(self):
        # gac.generate() returns None when the Armory it reads has no armour (or is not a
        # directory at all); that must be a finding, not a silent pass.
        import generate_armour_classes as gac
        import rebalance_armor as ra
        with tempfile.TemporaryDirectory() as tmp:
            with mock.patch.object(ra, "ARMORY_DIR", self._expected_root(tmp)), \
                 mock.patch.object(gac, "generate", return_value=None):
                issues = vm.armour_class_table_issues(self._armory(tmp))
        self.assertEqual([i.code for i in issues], [vm.ARMOUR_CLASS_CODE])
        self.assertIs(issues[0].severity, ts.Severity.WARNING)
        self.assertIn("NOT verified", issues[0].message)

    def test_generator_reading_a_different_armory_is_reported_not_passed(self):
        # gac.generate() reads ra.ARMORY_DIR, which honours only $BANNERLORD_GAME_DIR and
        # ignores --game-modules / $BANNERLORD_GAME_MODULES (tools/_gamedir.py). A caller who
        # pointed this pass at one install while the generator reads another must not have its
        # (perfectly self-consistent) rows read as a clean verification of THIS install.
        import generate_armour_classes as gac
        import rebalance_armor as ra
        with tempfile.TemporaryDirectory() as tmp:
            wrong_root = str(Path(tmp) / "SomeOtherArmory" / "ModuleData" / "LOTRLOME_items")
            with mock.patch.object(ra, "ARMORY_DIR", wrong_root), \
                 mock.patch.object(gac, "generate", return_value={"a_piece": ("light", None)}):
                issues = vm.armour_class_table_issues(self._armory(tmp))
        self.assertEqual([i.code for i in issues], [vm.ARMOUR_CLASS_CODE])
        self.assertIs(issues[0].severity, ts.Severity.WARNING)
        self.assertIn("NOT verified", issues[0].message)

    def test_malformed_committed_table_is_reported_as_drift_not_a_crash(self):
        # A merge conflict left in the committed armour_classes.xml must not crash the pass;
        # gac.drift_summary (fixed alongside this) reports it as a name-able kind of drift.
        import generate_armour_classes as gac
        import rebalance_armor as ra
        rows = {"a_piece": ("light", None)}
        with tempfile.TemporaryDirectory() as tmp:
            with mock.patch.object(ra, "ARMORY_DIR", self._expected_root(tmp)), \
                 mock.patch.object(gac, "generate", return_value=rows), \
                 mock.patch.object(gac, "read_committed", return_value="<<<<<<< HEAD\nnot xml"):
                issues = vm.armour_class_table_issues(self._armory(tmp))
        self.assertEqual([i.code for i in issues], [vm.ARMOUR_CLASS_CODE])
        self.assertIs(issues[0].severity, ts.Severity.WARNING)
        self.assertIn("does not parse", issues[0].message)

    # --- main()'s --code gate: a filtered run that excludes this code must not pay for it ---

    def _run_main_with_mocked_pass(self, tmp, *extra_args):
        """main()'s exit code, with armour_class_table_issues replaced by a spy so a test can
        assert whether it ran at all, never mind what it would have found."""
        root = Path(tmp)
        (root / "ModuleData").mkdir()
        with mock.patch.object(vm, "armour_class_table_issues", return_value=[]) as spy:
            _run_main("--moduledata", str(root / "ModuleData"), "--game-modules", str(root),
                     *extra_args)
        return spy

    def test_skipped_entirely_when_the_code_filter_excludes_it(self):
        # A filtered run discards the pass's issues anyway (main() applies --code to the whole
        # issue list); calling it just to throw the result away cost ~0.86s of every commit hook.
        with tempfile.TemporaryDirectory() as tmp:
            spy = self._run_main_with_mocked_pass(tmp, "--code", "SOME_OTHER_CODE")
        spy.assert_not_called()

    def test_runs_when_the_code_filter_names_it(self):
        with tempfile.TemporaryDirectory() as tmp:
            spy = self._run_main_with_mocked_pass(tmp, "--code", vm.ARMOUR_CLASS_CODE)
        spy.assert_called_once()

    def test_runs_when_no_code_filter_is_given(self):
        with tempfile.TemporaryDirectory() as tmp:
            spy = self._run_main_with_mocked_pass(tmp)
        spy.assert_called_once()


class FlagScopeTests(unittest.TestCase):
    """--moduledata and --game-modules must steer MELEE_LADDER_INVERSION and
    ARMOUR_CLASS_TABLE_DRIFT. Both used to read the repo's own tree and the default install
    whatever the flags said, so a run on another tree reported the repo's findings (#812)."""

    def _melee(self, md, game_modules=None):
        return ts.Validator(md, [], ts.Registries(items=set(), item_def_files={},
                            npccharacters=set(), cultures=set(), party_templates=set()),
                            game_modules=game_modules)._melee_ladder_inversions()

    def _need_install(self):
        import analyze_melee_ladder as mr
        if not mr.MODULES.is_dir() or mr.LET is None:
            self.skipTest("needs the Bannerlord install and lxml")
        return mr

    def test_tree_without_troops_checks_nothing_and_says_so(self):
        self._need_install()
        with tempfile.TemporaryDirectory() as tmp:
            issues = self._melee(Path(tmp))
        self.assertEqual([i.code for i in issues], ["MELEE_LADDER_INVERSION"])
        self.assertIn("checked nothing", issues[0].message)

    def test_findings_name_the_given_trees_troop_file(self):
        mr = self._need_install()
        # Gondor keeps findings the tier limit holds (its Numenorean bastard swords and greatswords).
        src = mr.TROOPS / "troops_gondor.xml"
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp)
            _write(md / "troops" / "troops_x.xml", src.read_text(encoding="utf-8-sig"))
            # Stale-exemption rows name the spec file, not a troop file; judge the rest.
            issues = [i for i in self._melee(md) if i.file != "tools/melee_ladders.json"]
        self.assertTrue(issues)
        self.assertEqual({i.file for i in issues}, {"troops/troops_x.xml"})

    def test_missing_game_modules_skips_the_melee_pass(self):
        self._need_install()
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(self._melee(Path(tmp), Path(tmp) / "nope"), [])

    def test_armour_class_table_not_verified_for_another_moduledata(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "LOTRLOME_Armory").mkdir()
            other = Path(tmp) / "ModuleData"
            other.mkdir()
            issues = vm.armour_class_table_issues(Path(tmp), other)
        self.assertEqual([i.code for i in issues], [vm.ARMOUR_CLASS_CODE])
        self.assertIn("NOT verified", issues[0].message)
        self.assertIn(str(other), issues[0].message)

    def test_missing_install_announces_the_melee_skip(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "ModuleData").mkdir()
            _code, out, err = _run_main("--moduledata", str(Path(tmp) / "ModuleData"),
                                        "--game-modules", str(Path(tmp) / "nope"))
        self.assertIn("MELEE_LADDER_INVERSION SKIPPED", err)
        # The skip must be real: no fallback to the default install's pricing.
        self.assertNotIn("MELEE_LADDER_INVERSION", out)


class SchemaInvalidPassTests(unittest.TestCase):
    """The engine-XSD layer (validate_xml_schemas.py) as a validator pass, so the commit hook
    gates it: a file the repo module registers that breaks its engine schema is an ERROR at
    the offending line, a registration that loads nothing is an ERROR against SubModule.xml,
    and a run that could not check the layer says so once instead of passing silently."""

    def setUp(self):
        vx._schema.cache_clear()
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.module = root / "Mod"
        self.schemas = root / "Game" / "XmlSchemas"
        _write(self.schemas / "Factions.xsd", _FACTIONS_XSD)
        _write(self.module / "SubModule.xml", _REGISTRATION)
        self.clans = self.module / "ModuleData" / "characters" / "clans.xml"
        _write(self.clans, _GOOD_CLANS)

    def tearDown(self):
        self._tmp.cleanup()

    def _issues(self, schemas=None):
        return vm.schema_invalid_issues(self.module, schemas or self.schemas)

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_no_install_given_is_a_not_verified_warning(self):
        """The MCP may run with no install path at all; the layer must say it was not checked."""
        issues = vm.schema_invalid_issues(self.module, None)
        self.assertEqual([(i.code, i.severity) for i in issues], [(vm.SCHEMA_CODE, ts.Severity.WARNING)])
        self.assertIn("no game install", issues[0].message)
        self.assertIn("NOT verified", issues[0].message)

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_schema_invalid_file_is_an_error_at_its_line(self):
        _write(self.clans, _GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', ""))
        issues = self._issues()
        self.assertEqual([(i.code, i.severity, i.file, i.line, i.entry_id) for i in issues],
                         [(vm.SCHEMA_CODE, ts.Severity.ERROR, "characters/clans.xml", 3, "Factions")])
        self.assertIn("initial_home_settlement", issues[0].message)
        self.assertEqual(vm.SCHEMA_CODE, "SCHEMA_INVALID")

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_rerun_hint_is_the_repo_relative_path(self):
        _write(self.clans, _GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', ""))
        with mock.patch.object(vm, "REPO_ROOT", Path(self._tmp.name)):
            issues = self._issues()
        self.assertTrue(issues[0].message.endswith(
            "python tools/validate_xml_schemas.py Mod/ModuleData/characters/clans.xml"), issues[0].message)

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_clean_module_has_no_issues(self):
        self.assertEqual(self._issues(), [])

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_registration_that_loads_nothing_is_an_error_against_submodule(self):
        _write(self.module / "SubModule.xml", _REGISTRATION.replace("characters/clans", "typo_clans"))
        issues = self._issues()
        self.assertEqual([(i.code, i.severity, i.file) for i in issues],
                         [(vm.SCHEMA_CODE, ts.Severity.ERROR, "SubModule.xml")])
        self.assertIn("typo_clans", issues[0].message)

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_malformed_submodule_is_an_error_not_a_traceback(self):
        _write(self.module / "SubModule.xml", "<Module>\n<Xmls>")
        issues = self._issues()
        self.assertEqual([(i.code, i.severity, i.file) for i in issues],
                         [(vm.SCHEMA_CODE, ts.Severity.ERROR, "SubModule.xml")])
        self.assertIn("not well-formed", issues[0].message)

    def test_absent_schema_folder_is_one_not_verified_warning(self):
        issues = self._issues(self.schemas.parent / "nowhere")
        self.assertEqual([(i.code, i.severity) for i in issues], [(vm.SCHEMA_CODE, ts.Severity.WARNING)])
        self.assertIn("NOT verified", issues[0].message)

    def test_missing_lxml_is_one_not_verified_warning(self):
        with mock.patch.object(vx, "HAVE_LXML", False):
            issues = self._issues()
        self.assertEqual([(i.code, i.severity) for i in issues], [(vm.SCHEMA_CODE, ts.Severity.WARNING)])
        self.assertIn("lxml", issues[0].message)
        self.assertIn("NOT verified", issues[0].message)

    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_main_runs_the_pass(self):
        """A pass main() never calls is a dead gate however well its unit tests pass.
        The schemas folder is found beside --game-modules, as in a real install."""
        _write(self.clans, _GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', ""))
        code, out, _ = _run_main("--moduledata", str(self.module / "ModuleData"),
                                 "--game-modules", str(self.schemas.parent / "Modules"),
                                 "--code", vm.SCHEMA_CODE)
        self.assertEqual(code, 1, out)
        self.assertIn(vm.SCHEMA_CODE, out)
        self.assertIn("characters/clans.xml:3", out)


class ItemSlotRejectedTests(unittest.TestCase):
    """#729: `Equipment.DeserializeNode` assigns an item only when `IsItemFitsToSlot` passes and
    otherwise asserts and leaves the slot empty. Two Uruk troops shipped BodyArmor in the Cape slot
    and nothing noticed. The slot rule is audit_battle_equipment_memory's; this pass only turns its
    refusals into ERRORs."""

    ITEMS = ('<Items>\n'
             '  <Item id="body_a" Type="BodyArmor" />\n'
             '  <Item id="cape_a" Type="Cape" />\n'
             '</Items>\n')

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        self.taom = self.modules / "TAOM"
        self.moduledata = self.taom / "ModuleData"
        for name in ("Native", "SandBoxCore", "SandBox"):
            self._module(self.modules / name, "")
        self._module(self.modules / "LOTRLOME_Armory",
                     '<XmlNode><XmlName id="Items" path="items" /></XmlNode>')
        _write(self.modules / "LOTRLOME_Armory" / "ModuleData" / "items.xml", self.ITEMS)
        self._module(self.taom,
                     '<XmlNode><XmlName id="NPCCharacters" path="troops" /></XmlNode>'
                     '<XmlNode><XmlName id="EquipmentRosters" path="rosters" /></XmlNode>')

    @staticmethod
    def _module(folder: Path, nodes: str) -> None:
        _write(folder / "SubModule.xml", f'<Module><Xmls>{nodes}</Xmls></Module>')
        (folder / "ModuleData").mkdir(parents=True, exist_ok=True)

    def _troops(self, body: str) -> None:
        _write(self.moduledata / "troops.xml", f"<NPCCharacters>\n{body}\n</NPCCharacters>\n")

    @staticmethod
    def _troop(troop_id: str, rows: str, extra: str = "") -> str:
        return (f'<NPCCharacter id="{troop_id}" culture="Culture.empire">'
                f'<Equipments>{extra}<EquipmentRoster>{rows}</EquipmentRoster></Equipments>'
                f'</NPCCharacter>')

    def _run(self) -> list:
        return vm.slot_rejection_issues(self.modules, self.moduledata)

    def test_a_refused_row_is_one_error_naming_troop_slot_and_item(self):
        self._troops(self._troop("bad_troop", '<equipment slot="Cape" id="Item.body_a" />'))
        issues = self._run()
        self.assertEqual([(i.code, i.severity, i.entry_id) for i in issues],
                         [(vm.SLOT_CODE, ts.Severity.ERROR, "bad_troop")])
        self.assertIn("Cape", issues[0].message)
        self.assertIn("body_a", issues[0].message)
        self.assertIn("BodyArmor", issues[0].message)
        self.assertIn("Body", issues[0].message.split("move it to the")[1])

    def test_the_issue_names_the_repo_file(self):
        self._troops(self._troop("bad_troop", '<equipment slot="Cape" id="Item.body_a" />'))
        self.assertEqual(self._run()[0].file, "Main/_Module/ModuleData/troops.xml")

    def test_a_fitting_row_gives_nothing(self):
        self._troops(self._troop("good_troop", '<equipment slot="Body" id="Item.body_a" />'
                                                '<equipment slot="Cape" id="Item.cape_a" />'))
        self.assertEqual(self._run(), [])

    def test_an_unknown_item_id_is_left_to_broken_item_ref(self):
        self._troops(self._troop("t", '<equipment slot="Cape" id="Item.no_such_item" />'))
        self.assertEqual(self._run(), [])

    def test_a_refusal_in_a_civilian_set_is_reported(self):
        self._troops('<NPCCharacter id="civ"><Equipments><EquipmentRoster civilian="true">'
                     '<equipment slot="Cape" id="Item.body_a" /></EquipmentRoster></Equipments>'
                     '</NPCCharacter>')
        self.assertEqual([i.entry_id for i in self._run()], ["civ"])

    def test_a_refusal_through_a_standalone_roster_is_reported(self):
        _write(self.moduledata / "rosters.xml",
               '<EquipmentRosters><EquipmentRoster id="r1"><EquipmentSet>'
               '<equipment slot="Cape" id="Item.body_a" /></EquipmentSet></EquipmentRoster>'
               '</EquipmentRosters>')
        self._troops('<NPCCharacter id="via_roster"><Equipments><EquipmentSet id="r1" />'
                     '</Equipments></NPCCharacter>')
        self.assertEqual([i.entry_id for i in self._run()], ["via_roster"])

    def test_a_refusal_through_a_troop_level_override_is_reported(self):
        self._troops(self._troop("overridden", '<equipment slot="Body" id="Item.body_a" />',
                                 extra='<equipment slot="Cape" id="Item.body_a" />'))
        issues = self._run()
        self.assertEqual([i.entry_id for i in issues], ["overridden"])
        self.assertIn("Cape", issues[0].message)

    def test_a_refusal_in_another_module_is_reported_too(self):
        self._module(self.modules / "LOTRLOME_Armory",
                     '<XmlNode><XmlName id="Items" path="items" /></XmlNode>'
                     '<XmlNode><XmlName id="NPCCharacters" path="chars" /></XmlNode>')
        _write(self.modules / "LOTRLOME_Armory" / "ModuleData" / "chars.xml",
               "<NPCCharacters>" + self._troop("armory_troop",
                                               '<equipment slot="Cape" id="Item.body_a" />')
               + "</NPCCharacters>")
        self._troops("")
        issues = self._run()
        self.assertEqual([i.entry_id for i in issues], ["armory_troop"])
        self.assertEqual(issues[0].file, "LOTRLOME_Armory/chars.xml")

    def test_a_missing_module_is_a_warning_not_a_pass(self):
        import shutil
        shutil.rmtree(self.modules / "SandBox")
        self._troops(self._troop("good_troop", '<equipment slot="Body" id="Item.body_a" />'))
        issues = self._run()
        self.assertEqual([(i.code, i.severity) for i in issues],
                         [(vm.SLOT_CODE, ts.Severity.WARNING)])
        self.assertIn("NOT verified", issues[0].message)
        self.assertIn("SandBox", issues[0].message)

    def test_main_walks_the_item_refs_once_for_both_collision_passes(self):
        # The walk over the Armory's XML and this repo's cost about 5 s of a validator run, twice.
        import validate_mesh_refs as vmr
        (self.modules / "LOTRLOME_Armory" / "ModuleData").mkdir(parents=True, exist_ok=True)
        with mock.patch.object(vm, "generator_item_ref_issues", return_value=[]), \
             mock.patch.object(vm, "skill_template_mismatch_issues", return_value=[]), \
             mock.patch.object(vm, "armour_class_table_issues", return_value=[]), \
             mock.patch.object(vm, "_loaded_tpacs", return_value=[Path("a.tpac")]), \
             mock.patch.object(vmr, "module_tpacs", return_value=[Path("a.tpac")]), \
             mock.patch.object(vmr, "build_present_set", return_value=vmr.PresentSet(tpac_paths=["a.tpac"])), \
             mock.patch.object(vmr, "classify", return_value=[]), \
             mock.patch.object(vmr, "extract_refs", return_value=[]) as walk:
            _run_main("--moduledata", str(self.moduledata), "--game-modules", str(self.modules), "--code", vm.SLOT_CODE)
        self.assertEqual(walk.call_count, 2, "one walk of the Armory and one of the repo")

    def test_main_runs_the_pass_and_exits_1_on_a_refusal(self):
        self._troops(self._troop("bad_troop", '<equipment slot="Cape" id="Item.body_a" />'))
        with mock.patch.object(vm, "generator_item_ref_issues", return_value=[]), \
             mock.patch.object(vm, "missing_collision_body_issues", return_value=[]), \
             mock.patch.object(vm, "borrowed_body_issues", return_value=[]), \
             mock.patch.object(vm, "skill_template_mismatch_issues", return_value=[]), \
             mock.patch.object(vm, "armour_class_table_issues", return_value=[]):
            code, out, _err = _run_main("--moduledata", str(self.moduledata),
                                        "--game-modules", str(self.modules),
                                        "--code", vm.SLOT_CODE)
        self.assertIn("bad_troop", out)
        self.assertEqual(code, 1)

    def test_main_without_install_says_the_pass_was_skipped(self):
        code, _out, err = _run_main("--moduledata", str(self.moduledata),
                                    "--game-modules", str(self.modules / "nowhere"),
                                    "--code", vm.SLOT_CODE)
        self.assertIn(f"{vm.SLOT_CODE} SKIPPED", err)
        self.assertEqual(code, 2)


class NoInstallWarningTests(unittest.TestCase):
    def test_the_warning_names_every_ref_check_that_is_skipped(self):
        with tempfile.TemporaryDirectory() as td:
            md = Path(td) / "ModuleData"
            md.mkdir()
            _code, _out, err = _run_main("--moduledata", str(md), "--game-modules", str(Path(td) / "nowhere"),
                                         "--code", "BROKEN_ROSTER_REF")
        warning = err.split("SKIPPED", 1)[0]
        for code in ("BROKEN_ITEM_REF", "BROKEN_ROSTER_REF", "BROKEN_SETTLEMENT_REF"):
            self.assertIn(code, warning)


class BrokenRosterRefTests(unittest.TestCase):
    """BROKEN_ROSTER_REF. An <EquipmentSet id> naming no defined <EquipmentRoster> null-dereferences
    inside the NPCCharacters load, and the engine's empty catch then stops every character after it in
    the merged document from loading, with no crash at the typo (RCA 2026-10-04 M3)."""

    NPC = ('<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
           '  <NPCCharacter id="lord_x">\n    <Equipments>\n'
           '      <EquipmentSet id="{ref}" />\n    </Equipments>\n  </NPCCharacter>\n</NPCCharacters>\n')

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.md = self.root / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _found(self, rosters=("known_set",)):
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures=set(), party_templates=set(), equipment_rosters=set(rosters))
        issues = ts.Validator(self.md, self.schemas, regs).run()
        return [i for i in issues if i.code == "BROKEN_ROSTER_REF"]

    def test_a_misspelt_roster_id_is_an_error_with_its_line(self):
        _write(self.md / "characters" / "lords_test.xml", self.NPC.format(ref="knwon_set"))
        found = self._found()
        self.assertEqual(len(found), 1)
        self.assertIs(found[0].severity, ts.Severity.ERROR)
        self.assertEqual((found[0].file, found[0].line, found[0].entry_id),
                         ("characters/lords_test.xml", 5, "lord_x"))
        self.assertIn("knwon_set", found[0].message)

    def test_a_defined_roster_id_is_clean(self):
        _write(self.md / "characters" / "lords_test.xml", self.NPC.format(ref="known_set"))
        self.assertEqual(self._found(), [])

    def test_a_ref_in_an_xslt_is_read(self):
        _write(self.md / "lords.xslt",
               '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
               '  <xsl:template match="NPCCharacter[@id=\'lord_x\']/Equipments">\n'
               '    <EquipmentSet id="knwon_set" />\n  </xsl:template>\n</xsl:stylesheet>\n')
        self.assertEqual([i.file for i in self._found()], ["lords.xslt"])

    def test_a_commented_out_ref_is_ignored(self):
        _write(self.md / "characters" / "lords_test.xml",
               '<NPCCharacters>\n<!-- <EquipmentSet id="knwon_set" /> -->\n</NPCCharacters>\n')
        self.assertEqual(self._found(), [])

    def test_an_empty_registry_means_the_pass_is_unavailable(self):
        _write(self.md / "characters" / "lords_test.xml", self.NPC.format(ref="knwon_set"))
        self.assertEqual(self._found(rosters=()), [])

    def _registry(self, with_install=True):
        modules = self.root / "Modules"
        _write(self.md / "characters" / "rosters.xml",
               '<EquipmentRosters>\n  <EquipmentRoster id="taom_set"><equipment slot="Body" id="Item.a" /></EquipmentRoster>\n'
               '  <!-- <EquipmentRoster id="commented_set"></EquipmentRoster> -->\n</EquipmentRosters>\n')
        _write(self.md / "spkingdoms.xslt",
               '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
               '  <xsl:template match="EquipmentRoster[@id=\'stripped_set\']"/>\n</xsl:stylesheet>\n')
        # Vanilla writes the id on its own line after the element name.
        _write(modules / "SandBoxCore" / "ModuleData" / "sandboxcore_equipment_sets.xml",
               '<EquipmentRosters>\n  <EquipmentRoster\n    id="vanilla_set">\n  </EquipmentRoster>\n'
               '  <EquipmentRoster\n    id="stripped_set">\n  </EquipmentRoster>\n</EquipmentRosters>\n')
        return ts.build_registries(self.md, modules if with_install else None).equipment_rosters

    def test_the_registry_holds_taom_and_vanilla_rosters_but_not_commented_or_stripped_ones(self):
        rosters = self._registry()
        self.assertEqual(rosters, {"taom_set", "vanilla_set"})

    def test_the_registry_is_unavailable_without_an_install(self):
        self.assertEqual(self._registry(with_install=False), set())

    def test_a_roster_only_the_incompatible_navaldlc_defines_is_not_known(self):
        # SubModule.xml lists NavalDLC as incompatible, so a roster only it defines is absent in game.
        _write(self.root / "Modules" / "NavalDLC" / "ModuleData" / "naval_equipment_sets.xml",
               '<EquipmentRosters>\n  <EquipmentRoster id="brother_char_creation_nord" />\n</EquipmentRosters>\n')
        self.assertNotIn("brother_char_creation_nord", self._registry())
        _write(self.md / "characters" / "lords_test.xml", self.NPC.format(ref="brother_char_creation_nord"))
        regs = ts.build_registries(self.md, self.root / "Modules")
        issues = ts.Validator(self.md, self.schemas, regs).run()
        self.assertEqual([i.code for i in issues if i.code == "BROKEN_ROSTER_REF"], ["BROKEN_ROSTER_REF"])


class BrokenSettlementRefTests(unittest.TestCase):
    """BROKEN_SETTLEMENT_REF. A clan's or kingdom's home and a village's bound town name a settlement in
    another, unversioned module, and no gate resolved those refs. A package pair where TAOM names a town
    its map lacks is how Arthedain nearly shipped (rca-ered-luin-2026-10-27.md row 2)."""

    CLAN = '<Factions>\n  <Faction id="clan_x" initial_home_settlement="Settlement.{ref}" />\n</Factions>\n'

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.md = self.root / "ModuleData"
        self.md.mkdir(parents=True)
        self.schemas = ts.load_schemas(SCHEMA_DIR)

    def tearDown(self):
        self._tmp.cleanup()

    def _found(self, settlements=("town_known",)):
        regs = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                             cultures=set(), party_templates=set(), settlements=set(settlements))
        issues = ts.Validator(self.md, self.schemas, regs).run()
        return [i for i in issues if i.code == "BROKEN_SETTLEMENT_REF"]

    def test_a_ref_to_no_settlement_is_an_error_with_its_line(self):
        _write(self.md / "characters" / "clans.xml", self.CLAN.format(ref="town_none"))
        found = self._found()
        self.assertEqual(len(found), 1)
        self.assertIs(found[0].severity, ts.Severity.ERROR)
        self.assertEqual((found[0].file, found[0].line), ("characters/clans.xml", 2))
        self.assertIn("town_none", found[0].message)

    def test_a_ref_to_a_settlement_is_clean(self):
        _write(self.md / "characters" / "clans.xml", self.CLAN.format(ref="town_known"))
        self.assertEqual(self._found(), [])

    def test_a_loc_key_that_starts_with_the_word_is_not_a_ref(self):
        _write(self.md / "strings.xml", '<strings><string id="Settlements.Settlement.name.x" text="y" /></strings>\n')
        self.assertEqual(self._found(), [])

    def test_the_two_settlement_files_the_game_never_loads_are_not_read(self):
        # The repo dropped them on 2026-11-14, but deploys never delete, so older installs keep them.
        for name in ("custom_settlements.xml", "settlements.xml"):
            _write(self.md / name, self.CLAN.format(ref="town_none"))
        self.assertEqual(self._found(), [])

    def test_a_file_of_that_name_in_another_folder_is_read(self):
        _write(self.md / "sub" / "settlements.xml", self.CLAN.format(ref="town_none"))
        self.assertEqual([i.file for i in self._found()], ["sub/settlements.xml"])

    def test_an_empty_registry_means_the_pass_is_unavailable(self):
        _write(self.md / "characters" / "clans.xml", self.CLAN.format(ref="town_none"))
        self.assertEqual(self._found(settlements=()), [])

    XSLT = ('<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
            '  <xsl:template match="Faction[@id=\'clan_x\']">\n%s'
            '  </xsl:template>\n</xsl:stylesheet>\n')

    def test_a_home_written_as_xsl_attribute_text_is_read(self):
        # spclans.xslt, spkingdoms.xslt and spcultures.xslt write 34 homes this way, as element text.
        _write(self.md / "spclans.xslt", self.XSLT %
               '    <xsl:attribute name="initial_home_settlement">Settlement.town_none</xsl:attribute>\n')
        found = self._found()
        self.assertEqual([(i.file, i.line) for i in found], [("spclans.xslt", 3)])
        self.assertIs(found[0].severity, ts.Severity.ERROR)
        self.assertIn("town_none", found[0].message)

    def test_a_home_written_as_an_attribute_value_in_an_xslt_is_read(self):
        _write(self.md / "spkingdoms.xslt", self.XSLT %
               '    <Faction initial_home_settlement="Settlement.town_none" />\n')
        self.assertEqual([i.file for i in self._found()], ["spkingdoms.xslt"])

    def test_a_known_home_in_an_xslt_is_clean(self):
        _write(self.md / "spclans.xslt", self.XSLT %
               '    <xsl:attribute name="initial_home_settlement">Settlement.town_known</xsl:attribute>\n')
        self.assertEqual(self._found(), [])

    def test_a_loc_key_in_an_xslt_is_not_a_ref(self):
        _write(self.md / "spcultures.xslt", self.XSLT %
               '    <xsl:attribute name="name">{=Settlements.Settlement.name.x}Town</xsl:attribute>\n'
               '    <xsl:text>{=Settlements.Settlement.name.y}</xsl:text>\n'
               '    <xsl:text>"{=Settlements.Settlement.name.z}"</xsl:text>\n')
        self.assertEqual(self._found(), [])

    def test_a_commented_out_xslt_home_is_ignored_and_lines_hold(self):
        _write(self.md / "spclans.xslt", self.XSLT %
               '    <!-- the old home\n         <xsl:attribute name="initial_home_settlement">Settlement.town_gone</xsl:attribute> -->\n'
               '    <xsl:attribute name="initial_home_settlement">Settlement.town_none</xsl:attribute>\n')
        self.assertEqual([i.line for i in self._found()], [5])

    def test_the_xslt_pass_reads_only_the_tools_own_moduledata(self):
        _write(self.root / "other" / "spclans.xslt", self.XSLT %
               '    <xsl:attribute name="initial_home_settlement">Settlement.town_none</xsl:attribute>\n')
        self.assertEqual(self._found(), [])

    def test_a_commented_out_xml_ref_is_ignored_and_lines_hold(self):
        # A commented-out Faction row must not fail a good channel or the commit gate.
        _write(self.md / "characters" / "clans.xml",
               '<Factions>\n  <!-- <Faction id="clan_old" initial_home_settlement="Settlement.town_old"/>\n'
               '       second comment line -->\n'
               '  <Faction id="clan_x" initial_home_settlement="Settlement.town_none" />\n</Factions>\n')
        found = self._found()
        self.assertEqual([(i.file, i.line) for i in found], [("characters/clans.xml", 4)])
        self.assertIn("town_none", found[0].message)

    def test_a_commented_out_ref_of_every_other_kind_is_ignored_too(self):
        _write(self.md / "characters" / "lords_test.xml",
               '<NPCCharacters>\n  <!-- <equipment id="Item.ghost"/> <x culture="Culture.ghost"/>\n'
               '       <x troop="NPCCharacter.ghost"/> <x f="Faction.ghost"/> -->\n</NPCCharacters>\n')
        regs = ts.Registries(items={"real"}, item_def_files={}, npccharacters={"real"},
                             cultures={"real"}, party_templates=set(), factions={"real"})
        broken = [i for i in ts.Validator(self.md, self.schemas, regs).run()
                  if i.code in ("BROKEN_ITEM_REF", "BROKEN_TROOP_REF", "UNKNOWN_CULTURE", "BROKEN_FACTION_REF")]
        self.assertEqual(broken, [])

    def _registry(self, strip=True):
        modules = self.root / "Modules"
        _write(modules / "SandBox" / "ModuleData" / "settlements.xml",
               '<Settlements>\n  <Settlement id="town_vanilla" />\n</Settlements>\n')
        _write(modules / "TAOM_Map" / "ModuleData" / "settlements.xml",
               '<Settlements>\n  <Settlement\n    id="town_map" />\n  <!-- <Settlement id="town_commented" /> -->\n'
               '</Settlements>\n')
        if strip:
            _write(modules / "TAOM_Map" / "ModuleData" / "settlements.xslt",
                   '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
                   '  <xsl:template match="Settlement"/>\n</xsl:stylesheet>\n')
        return ts.build_registries(self.md, modules).settlements

    def test_the_registry_is_the_world_the_game_builds(self):
        self.assertEqual(self._registry(), {"town_map"})

    def test_the_registry_keeps_vanilla_rows_when_the_map_does_not_strip_them(self):
        self.assertEqual(self._registry(strip=False), {"town_map", "town_vanilla"})

    def test_the_registry_is_unavailable_without_an_install(self):
        self.assertEqual(ts.build_registries(self.md, None).settlements, set())

    def test_vanilla_cultures_can_come_from_a_second_modules_root(self):
        # Culture.nord defined by the install must not read UNKNOWN_CULTURE on a channel that holds no SandBox.
        channel, install = self.root / "channel", self.root / "install"
        channel.mkdir()
        _write(install / "SandBox" / "ModuleData" / "spcultures.xml",
               '<SPCultures>\n  <Culture id="install_only_culture" />\n</SPCultures>\n')
        self.assertNotIn("install_only_culture", ts.build_registries(self.md, channel).cultures)
        self.assertIn("install_only_culture",
                      ts.build_registries(self.md, channel, vanilla_modules=install).cultures)

    def test_vanilla_clans_can_come_from_a_second_modules_root(self):
        # A release channel holds no SandBox: its clan registry needs the install's.
        channel, install = self.root / "channel", self.root / "install"
        channel.mkdir()
        _write(install / "SandBox" / "ModuleData" / "spclans.xml",
               '<Factions>\n  <Faction id="clan_vanilla" />\n</Factions>\n')
        self.assertEqual(ts.build_registries(self.md, channel).factions, set())
        self.assertEqual(ts.build_registries(self.md, channel, vanilla_modules=install).factions, {"clan_vanilla"})


class CommitGateCoverageTests(unittest.TestCase):
    """The commit hook filters `validate_moduledata.py` down to an explicit
    `--code` allowlist, so an ERROR the validator can emit is only ever enforced
    if someone remembered to add it to that list.

    On 2026-09-01 four of nine ERROR codes were missing from it, including the
    freshly added MISSING_BODY_ARMOUR. Nothing detected that, because the two
    lists live in different files and neither refers to the other. This test is
    the reference between them: add an ERROR code and you must wire it, or this
    fails and tells you which one.
    """

    HOOK = Path(__file__).resolve().parents[2] / ".claude" / "hooks" / "check-moduledata-validation.sh"
    SCHEMA = Path(ts.__file__)
    VALIDATOR = Path(vm.__file__)

    @staticmethod
    def _named_codes(src, severity):
        """Codes emitted through a module constant: `code=BODY_CODE` where
        `BODY_CODE = "MISSING_COLLISION_BODY"` is defined at module level."""
        consts = dict(re.findall(r'^([A-Z_]+_CODE)\s*=\s*"([A-Z_]+)"', src, flags=re.M))
        used = re.findall(r'severity=(?:ts\.)?Severity\.' + severity + r',\s*code=([A-Z_]+_CODE)\b', src)
        return {consts[c] for c in used if c in consts}

    @staticmethod
    def _unreached_passes(src):
        """Every module-level `def <x>_issues(` whose call does not appear in run_all_passes()'s body,
        the one composition main() and the MCP tool share (#623)."""
        passes = re.findall(r"^def (\w+_issues)\(", src, flags=re.M)
        body = re.search(r"^def run_all_passes\(.*?(?=^\S)", src, flags=re.M | re.S).group(0)
        return passes, [p for p in passes if f"{p}(" not in body]

    def test_every_issue_pass_is_reached_from_run_all_passes(self):
        """#622: missing_collision_body_issues was defined, documented, in the hook's --code
        list and unit-tested, and main() never called it for three days. The code-string
        checks in this class prove presence in source, not reachability; this checks the call."""
        passes, unreached = self._unreached_passes(self.VALIDATOR.read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(passes), 3, "the pass scan found nothing")
        self.assertEqual(unreached, [], f"run_all_passes() never calls: {unreached}")

    def test_main_and_the_mcp_tool_both_call_run_all_passes(self):
        """#623: the MCP's validate ran the schema engine alone while main() ran every pass. Both
        compose through one function, so a pass added there reaches both."""
        src = self.VALIDATOR.read_text(encoding="utf-8")
        main_body = re.search(r"^def main\(\).*?(?=^\S)", src, flags=re.M | re.S).group(0)
        self.assertIn("run_all_passes(", main_body)
        query = (Path(__file__).resolve().parents[1] / "taom_query.py").read_text(encoding="utf-8")
        body = re.search(r"^    def validate\(.*?(?=^    \S|^\S|\Z)", query, flags=re.M | re.S)
        self.assertIsNotNone(body, "taom_query.py has no ModuleDataQuery.validate")
        self.assertIn("run_all_passes(", body.group(0))

    @staticmethod
    def _error_codes_in(src):
        """ERROR codes from the shapes the module uses: an inline Issue(...), the ref-sweep
        table whose rows are `(..., Severity.ERROR, "CODE", ...)`, a `*_CODE` constant, and a
        `*_SEVERITY = Severity.ERROR` constant passed as `severity=`. Scanning only the inline
        form is how the first version of this test read 7 live codes as non-existent."""
        return (set(re.findall(r'severity=(?:ts\.)?Severity\.ERROR,\s*code="([A-Z_]+)"', src))
                | set(re.findall(r'Severity\.ERROR,\s*"([A-Z_]+)"', src))
                | CommitGateCoverageTests._named_codes(src, "ERROR")
                | CommitGateCoverageTests._severity_constant_codes(src, "ERROR"))

    @staticmethod
    def _severity_constant_codes(src, severity):
        """Codes emitted as `severity=NAME_SEVERITY, code=...` where the module sets
        `NAME_SEVERITY = Severity.<severity>`; the code is a literal or a `*_CODE` constant."""
        names = re.findall(r'^([A-Z_]+_SEVERITY)\s*=\s*(?:ts\.)?Severity\.' + severity + r'\b', src, flags=re.M)
        consts = dict(re.findall(r'^([A-Z_]+_CODE)\s*=\s*"([A-Z_]+)"', src, flags=re.M))
        out = set()
        for name in names:
            for literal, const in re.findall(r'severity=' + name + r',\s*code=(?:"([A-Z_]+)"|([A-Z_]+_CODE)\b)', src):
                out.add(literal or consts.get(const, ""))
        out.discard("")
        return out

    def _error_codes(self):
        # Both files emit codes: taom_schema.py owns the ref sweep, and
        # validate_moduledata.py adds the passes that call sibling tools
        # (generators, collision bodies). Scanning one of them is how a code
        # could be emitted and never enforced (#599 added the first ERROR to the
        # second file).
        src = self.SCHEMA.read_text(encoding="utf-8") + self.VALIDATOR.read_text(encoding="utf-8")
        return self._error_codes_in(src)

    def test_a_severity_constant_flipped_to_error_makes_its_code_an_enforced_code(self):
        """WORN_ARMOUR_CEILING_SEVERITY is the one switch that turns the ceiling into an error;
        the scan has to see that flip or the hook list is never checked against it."""
        src = self.SCHEMA.read_text(encoding="utf-8")
        switch = re.compile(r"^(WORN_ARMOUR_CEILING_SEVERITY\s*=\s*)Severity\.\w+", re.M)
        self.assertRegex(src, switch)
        self.assertNotIn("WORN_ARMOUR_CEILING", self._error_codes_in(switch.sub(r"\1Severity.WARNING", src)))
        self.assertIn("WORN_ARMOUR_CEILING", self._error_codes_in(switch.sub(r"\1Severity.ERROR", src)))

    def test_a_severity_constant_resolves_for_a_literal_code_and_a_code_constant_alike(self):
        src = ('SLOW_SEVERITY = Severity.ERROR\n'
               'LAX_SEVERITY = Severity.WARNING\n'
               'FAST_CODE = "FAST_ONE"\n'
               'a = Issue(severity=SLOW_SEVERITY, code="SLOW_ONE")\n'
               'b = Issue(severity=SLOW_SEVERITY, code=FAST_CODE)\n'
               'c = Issue(severity=LAX_SEVERITY, code="LAX_ONE")\n')
        self.assertEqual(self._error_codes_in(src), {"SLOW_ONE", "FAST_ONE"})

    def _hook_codes(self):
        return set(re.findall(r"--code\s+([A-Z_]+)", self.HOOK.read_text(encoding="utf-8")))

    def test_the_hook_exists_and_names_codes(self):
        """Guards the guard: if the hook is renamed or the --code form changes,
        the two tests below would pass vacuously on empty sets."""
        self.assertTrue(self.HOOK.is_file(), f"missing {self.HOOK}")
        self.assertGreaterEqual(len(self._hook_codes()), 10)
        self.assertGreaterEqual(len(self._error_codes()), 5)

    def test_every_error_code_is_enforced_by_the_commit_hook(self):
        missing = sorted(self._error_codes() - self._hook_codes())
        self.assertEqual(
            missing, [],
            "these ERROR codes can be emitted but are filtered out of the commit "
            f"gate, so they never block a commit: {missing}. Add a --code line to "
            f"{self.HOOK.name}")

    def test_the_hook_names_no_code_the_validator_cannot_emit(self):
        """A typo'd or retired code in the hook is a silently dead gate line."""
        src = self.SCHEMA.read_text(encoding="utf-8") + self.VALIDATOR.read_text(encoding="utf-8")
        emitted = (set(re.findall(r'code="([A-Z_]+)"', src))
                   | set(re.findall(r'Severity\.(?:ERROR|WARNING),\s*"([A-Z_]+)"', src))
                   | self._named_codes(src, "ERROR") | self._named_codes(src, "WARNING")
                   # the duplicate-id family is built as f"DUPLICATE_{kind}_ID"
                   | {"DUPLICATE_NPC_ID", "DUPLICATE_CULTURE_ID", "DUPLICATE_ROSTER_ID"})
        unknown = sorted(self._hook_codes() - emitted)
        self.assertEqual(unknown, [], f"hook names codes the validator never emits: {unknown}")

    DOC = (Path(__file__).resolve().parents[2] / "docs" / "reference" / "moduledata-validator-codes.md")

    def test_every_emitted_code_has_a_row_in_the_codes_doc(self):
        """The validator-codes doc promises every code and what repairs it. A code with no
        row sends the reader to the source."""
        src = self.SCHEMA.read_text(encoding="utf-8") + self.VALIDATOR.read_text(encoding="utf-8")
        emitted = (set(re.findall(r'code="([A-Z_]+)"', src))
                   | set(re.findall(r'Severity\.(?:ERROR|WARNING),\s*"([A-Z_]+)"', src))
                   | self._named_codes(src, "ERROR") | self._named_codes(src, "WARNING")
                   | {"DUPLICATE_NPC_ID", "DUPLICATE_CULTURE_ID", "DUPLICATE_ROSTER_ID"})
        doc = self.DOC.read_text(encoding="utf-8")
        rows = set(re.findall(r"^\| `([A-Z_]+)`", doc, flags=re.M))
        if "| `DUPLICATE_{NPC,CULTURE,ROSTER}_ID`" in doc:
            rows |= {"DUPLICATE_NPC_ID", "DUPLICATE_CULTURE_ID", "DUPLICATE_ROSTER_ID"}
        self.assertGreaterEqual(len(rows), 20, "the doc scan found almost no rows")
        self.assertEqual(sorted(emitted - rows), [],
                         f"codes with no row in {self.DOC.name}: add a row to its Codes table")


class ReadStrippedCacheTests(unittest.TestCase):
    def test_the_second_sweep_of_many_files_hits_the_cache(self):
        """At maxsize=64 a validator run made 23,076 misses and 2 hits (2026-11-03): each sweep reads more
        files than the bound, so the least recently used entry is always the one read next."""
        import tempfile
        ts._read_stripped.cache_clear()
        with tempfile.TemporaryDirectory() as d:
            files = [Path(d) / f"{i}.xml" for i in range(200)]
            for f in files:
                f.write_text("<a/>", encoding="utf-8")
            for f in files:
                ts._read_stripped(f)
            before = ts._read_stripped.cache_info().hits
            for f in files:
                ts._read_stripped(f)
            self.assertEqual(ts._read_stripped.cache_info().hits - before, 200)
        ts._read_stripped.cache_clear()


class ArmourAcquisitionRefTests(unittest.TestCase):
    """ARMOUR_ACQUISITION_REF: an id or culture the armour acquisition data names that nothing defines
    fails silently in game: a commission that never fires, a reward that never arrives, a named weapon
    left on sale, a culture that draws on no armour (docs/features/armour-acquisition.md). Each is an
    ERROR; the metals the engine registers in code (DefaultItems) are known; a file that does not parse
    is reported, never passed."""

    ISSUES = "lotr_issues/taom_lotr_issues.xml"
    CONFIG = "armour_acquisition/armour_acquisition_config.xml"
    MARKET = "culture_marketplace/culture_marketplace_config.xml"
    QUESTS = "career_system/taom_career_quests.xml"

    # The lord's gear ladder (#693): its rung quests, lord's materials, weapon picks and named armour pieces.
    LADDER = ('<LordsLadder><Step slot="hands" quest="q_hands" materials="10" /></LordsLadder>'
              '<LordsMaterials><Material culture="gondor" item="lm_gondor" /></LordsMaterials>'
              '<LadderWeapons><Weapon culture="gondor" item="anduril" /></LadderWeapons>'
              '<LadderPieces><Piece culture="gondor" slot="head" item="riv_chest" /></LadderPieces>')

    CLEAN = {
        ISSUES: '<LotrIssues><LotrIssue id="c1" cultures="lindon,rivendell" reward_item="riv_chest" '
                'item_source="item:ironIngot4" /><LotrIssue id="c2" cultures="" reward_item="" '
                'item_source="category:grain" /></LotrIssues>',
        CONFIG: '<ArmourAcquisition><Upgrades><Upgrade target="lord"><Material item="ironIngot6" count="6" />'
                '</Upgrade></Upgrades><NamedWeapons><Item id="anduril" /></NamedWeapons>' + LADDER + '</ArmourAcquisition>',
        MARKET: '<CultureMarketplaceConfig><Culture id="lindon" armour_from="rivendell" />'
                '</CultureMarketplaceConfig>',
        # career_id differs from id so a gate reading the wrong attribute fails.
        QUESTS: '<CareerQuests><CareerQuest id="q_hands" career_id="c_hands" tier="1" /></CareerQuests>',
    }

    def _config(self, ladder):
        return '<ArmourAcquisition>' + ladder + '</ArmourAcquisition>'

    def _run(self, **overrides):
        files = dict(self.CLEAN)
        files.update({getattr(self, k): v for k, v in overrides.items()})
        registries = mock.Mock(items={"riv_chest", "anduril", "lm_gondor"}, cultures={"rivendell", "lindon", "gondor"})
        with tempfile.TemporaryDirectory() as tmp:
            for rel, text in files.items():
                if text is None:
                    continue
                path = Path(tmp) / rel
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text, encoding="utf-8")
            return vm.armour_acquisition_ref_issues(registries, Path(tmp))

    def _one(self, issues, needle):
        self.assertEqual(len(issues), 1, [i.message for i in issues])
        self.assertEqual(issues[0].code, "ARMOUR_ACQUISITION_REF")
        self.assertEqual(issues[0].severity, vm.ts.Severity.ERROR)
        self.assertIn(needle, issues[0].message)

    def test_clean_data_raises_nothing(self):
        self.assertEqual(self._run(), [])

    def test_missing_files_raise_nothing(self):
        self.assertEqual(self._run(ISSUES=None, CONFIG=None, MARKET=None), [])

    def test_a_commission_culture_nothing_defines_is_an_error(self):
        self._one(self._run(ISSUES='<LotrIssues><LotrIssue id="c1" cultures="lindon, rohan" /></LotrIssues>'),
                  "'rohan'")

    def test_a_reward_item_nothing_defines_is_an_error(self):
        self._one(self._run(ISSUES='<LotrIssues><LotrIssue id="c1" reward_item="retired_chest" /></LotrIssues>'),
                  "'retired_chest'")

    def test_an_item_source_nothing_defines_is_an_error(self):
        self._one(self._run(ISSUES='<LotrIssues><LotrIssue id="c1" item_source="item:lost_ore" /></LotrIssues>'),
                  "'lost_ore'")

    def test_a_named_weapon_nothing_defines_is_an_error(self):
        self._one(self._run(CONFIG='<ArmourAcquisition><NamedWeapons><Item id="glamdring" />'
                                   '</NamedWeapons></ArmourAcquisition>'), "'glamdring'")

    def test_a_material_nothing_defines_is_an_error_but_the_engines_metals_are_known(self):
        self._one(self._run(CONFIG='<ArmourAcquisition><Upgrades><Upgrade target="heavy">'
                                   '<Material item="ironIngot4" count="3" /><Material item="mithril" count="1" />'
                                   '</Upgrade></Upgrades></ArmourAcquisition>'), "'mithril'")

    def test_a_stock_row_culture_nothing_defines_is_an_error(self):
        self._one(self._run(MARKET='<CultureMarketplaceConfig><Culture id="lindon">'
                                   '<Stock from="gondr" /></Culture></CultureMarketplaceConfig>'), "'gondr'")

    def test_a_stock_match_that_does_not_compile_is_an_error(self):
        self._one(self._run(MARKET='<CultureMarketplaceConfig><Culture id="lindon">'
                                   '<Stock match="[unclosed" /></Culture></CultureMarketplaceConfig>'), "does not compile")

    def test_a_stock_match_that_finds_no_item_is_an_error(self):
        self._one(self._run(MARKET='<CultureMarketplaceConfig><Culture id="lindon">'
                                   '<Stock match="^sk_ar_art_" /></Culture></CultureMarketplaceConfig>'), "finds no item")

    def test_a_stock_match_that_finds_an_item_is_clean(self):
        self.assertEqual(self._run(MARKET='<CultureMarketplaceConfig><Culture id="lindon">'
                                          '<Stock from="rivendell" match="chest" /></Culture></CultureMarketplaceConfig>'), [])

    def test_an_armour_donor_nothing_defines_is_an_error(self):
        self._one(self._run(MARKET='<CultureMarketplaceConfig><Culture id="lindon" armour_from="rivendel" />'
                                   '</CultureMarketplaceConfig>'), "'rivendel'")

    def test_a_marketplace_culture_nothing_defines_is_an_error(self):
        self._one(self._run(MARKET='<CultureMarketplaceConfig><Culture id="lindonn" />'
                                   '</CultureMarketplaceConfig>'), "'lindonn'")

    def test_a_file_that_does_not_parse_is_reported_not_passed(self):
        self._one(self._run(CONFIG='<ArmourAcquisition><NamedWeapons>'), "NOT checked")

    def test_a_ladder_weapon_nothing_defines_is_an_error(self):
        issues = self._run(CONFIG=self._config('<LadderWeapons><Weapon culture="gondor" item="lost_sword" /></LadderWeapons>'))
        self._one(issues, "'lost_sword'")
        self.assertEqual(issues[0].entry_id, "LadderWeapons")

    def test_a_ladder_weapon_culture_nothing_defines_is_an_error(self):
        self._one(self._run(CONFIG=self._config('<LadderWeapons><Weapon culture="rohann" item="anduril" /></LadderWeapons>')),
                  "'rohann'")

    def test_a_ladder_piece_nothing_defines_is_an_error(self):
        issues = self._run(CONFIG=self._config('<LadderPieces><Piece culture="gondor" slot="head" item="lost_crown" /></LadderPieces>'))
        self._one(issues, "'lost_crown'")
        self.assertEqual(issues[0].entry_id, "LadderPieces")

    def test_a_ladder_piece_culture_nothing_defines_is_an_error(self):
        self._one(self._run(CONFIG=self._config('<LadderPieces><Piece culture="arthedan" slot="head" item="riv_chest" /></LadderPieces>')),
                  "'arthedan'")

    def test_a_lords_material_nothing_defines_is_an_error(self):
        issues = self._run(CONFIG=self._config('<LordsMaterials><Material culture="gondor" item="lost_ore" /></LordsMaterials>'))
        self._one(issues, "'lost_ore'")
        self.assertEqual(issues[0].entry_id, "LordsMaterials", "not an upgrade metal")

    def test_a_lords_material_culture_nothing_defines_is_an_error(self):
        self._one(self._run(CONFIG=self._config('<LordsMaterials><Material culture="rohann" item="lm_gondor" /></LordsMaterials>')),
                  "'rohann'")

    def test_a_ladder_rung_quest_nothing_defines_is_an_error(self):
        # The armoury starts a rung by its quest id: one the career quests do not define never starts.
        self._one(self._run(CONFIG=self._config('<LordsLadder><Step slot="head" quest="q_lost" materials="3" /></LordsLadder>')),
                  "'q_lost'")

    def test_ladder_rungs_without_the_career_quest_file_are_errors(self):
        issues = self._run(QUESTS=None)
        self._one(issues, "'q_hands'")

    def test_a_nested_rung_quest_is_not_a_definition(self):
        # The game reads only the root's own <CareerQuest> children (CareerQuestConfigProvider, root.Elements).
        self._one(self._run(QUESTS='<CareerQuests><Group><CareerQuest id="q_hands" career_id="c_hands" tier="1" />'
                                   '</Group></CareerQuests>'), "'q_hands'")

    def test_an_unparsable_quest_file_is_reported_once_not_per_rung(self):
        self._one(self._run(QUESTS='<CareerQuests>'), "does not parse")

    def test_a_file_with_an_unknown_encoding_is_reported_not_a_crash(self):
        self._one(self._run(CONFIG='<?xml version="1.0" encoding="utf8x"?><ArmourAcquisition />'), "NOT checked")

    def test_the_shipped_files_carry_every_element_the_gate_reads(self):
        # A section renamed in the XML and the C# together would leave the gate checking nothing.
        config = vm.ET.parse(vm.MODULEDATA / self.CONFIG).getroot()
        for path in ("Upgrades/Upgrade/Material", "NamedWeapons/Item", "LordsLadder/Step",
                     "LordsMaterials/Material", "LadderWeapons/Weapon", "LadderPieces/Piece"):
            self.assertTrue(config.findall(path), f"the shipped armour config has no {path}")
        quests = vm.ET.parse(vm.MODULEDATA / self.QUESTS).getroot()
        self.assertTrue(quests.findall("CareerQuest"), "the shipped career quests have no root-level <CareerQuest>")


class RunAllPassesParityTests(unittest.TestCase):
    """#623: the MCP tool's validate_moduledata ran ts.Validator alone, so an agent trusting it got no
    collision-body, engine-XSD, generator or skill-template verdict. main() and
    taom_query.ModuleDataQuery.validate now both call run_all_passes; these pin that they agree."""

    PASSES = re.findall(r"^def (\w+_issues)\(", Path(vm.__file__).read_text(encoding="utf-8"), flags=re.M)

    def test_the_cli_and_the_mcp_tool_report_the_same_codes(self):
        import taom_query as tq
        self.assertGreaterEqual(len(self.PASSES), 9, "the pass scan found almost nothing")
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            md = root / "ModuleData"
            _write(md / "empty.xml", "<NPCCharacters />")
            mods = root / "Game" / "Modules"
            for name in ("LOTRLOME_Armory", "TAOM_Map"):
                (mods / name / "ModuleData").mkdir(parents=True)
            cli_json = root / "cli.json"
            with contextlib.ExitStack() as stack:
                for name in self.PASSES:
                    stack.enter_context(mock.patch.object(vm, name, return_value=[ts.Issue(
                        severity=ts.Severity.WARNING, code=f"STUB_{name.upper()}", file="", line=0,
                        entry_id="", message=name)]))
                _run_main("--moduledata", str(md), "--game-modules", str(mods), "--json", str(cli_json))
                registries = ts.build_registries(md, mods)
                mcp = tq.ModuleDataQuery(md, registries, ts.load_schemas(SCHEMA_DIR),
                                         game_modules=mods).validate()
            cli_codes = {i["code"] for i in json.loads(cli_json.read_text(encoding="utf-8"))}
        mcp_codes = {i["code"] for i in mcp["issues"]} - {vm.PARTIAL_RUN_CODE}
        self.assertEqual(mcp_codes, cli_codes)
        self.assertLessEqual({f"STUB_{n.upper()}" for n in self.PASSES}, cli_codes,
                             "a pass was not run with the install present")

    def test_the_mcp_tool_returns_what_it_did_not_check_as_warnings(self):
        """The MCP has no stderr: a pass skipped for want of an install must come back as a WARNING."""
        import taom_query as tq
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp) / "ModuleData"
            _write(md / "empty.xml", "<NPCCharacters />")
            registries = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                                       cultures={"vlandia"}, party_templates=set())
            out = tq.ModuleDataQuery(md, registries, [], game_modules=Path(tmp) / "nowhere").validate(
                codes=[vm.BODY_CODE])
        notices = [i for i in out["issues"] if i["code"] == vm.PARTIAL_RUN_CODE]
        self.assertTrue(any(f"{vm.BODY_CODE} SKIPPED" in i["message"] for i in notices), out)
        self.assertTrue(all(i["severity"] == "WARNING" for i in notices))


if __name__ == "__main__":
    unittest.main(verbosity=2)
