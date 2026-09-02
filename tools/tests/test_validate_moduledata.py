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
import json
import os
import re
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import taom_schema as ts  # noqa: E402
import validate_moduledata as vm  # noqa: E402

SCHEMA_DIR = Path(__file__).resolve().parent.parent / "schemas"


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


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
  <NPCCharacter id="hero_a" default_group="Infantry" culture="Culture.gondor">
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
  <NPCCharacter id="hero_b" default_group="Cavalry" culture="Culture.gondor" />
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
        self.assertEqual([Path(p) for p in v.missing_ref_roots], [gone])

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


if __name__ == "__main__":
    unittest.main(verbosity=2)


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


class ArmoryStructuralAssumptionTests(unittest.TestCase):
    """The Armory's schema checks silently do not run, and that is only safe today.

    `item_roots` includes LOTRLOME_Armory, so its items and `<Monster>` decls reach
    the registries, but every schema pass iterates `Validator._xml_files()`, which is
    repo-only. Duplicate-id, enum, civilian `equipmentType`, harness pairing and
    MOUNTED_DWARF therefore never run against the Armory.

    That costs nothing purely because of what the Armory currently contains: items
    and monsters, nothing else. `/author-armor`'s workflow makes it plausible somebody
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
        """The three bare-chested troops that ship deliberately. Removing an id
        from _BODYLESS_BY_DESIGN is what makes this check start guarding it."""
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

    def _error_codes(self):
        """ERROR codes from BOTH shapes the module uses: an inline Issue(...) and
        the ref-sweep table, whose rows are `(..., Severity.ERROR, "CODE", ...)`.
        Scanning only the inline form is how the first version of this test read
        7 live codes as non-existent."""
        src = self.SCHEMA.read_text(encoding="utf-8")
        return (set(re.findall(r'severity=Severity\.ERROR,\s*code="([A-Z_]+)"', src))
                | set(re.findall(r'Severity\.ERROR,\s*"([A-Z_]+)"', src)))

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
        src = self.SCHEMA.read_text(encoding="utf-8")
        emitted = (set(re.findall(r'code="([A-Z_]+)"', src))
                   | set(re.findall(r'Severity\.(?:ERROR|WARNING),\s*"([A-Z_]+)"', src))
                   # the duplicate-id family is built as f"DUPLICATE_{kind}_ID"
                   | {"DUPLICATE_NPC_ID", "DUPLICATE_CULTURE_ID", "DUPLICATE_ROSTER_ID"})
        unknown = sorted(self._hook_codes() - emitted)
        self.assertEqual(unknown, [], f"hook names codes the validator never emits: {unknown}")
